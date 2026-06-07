using UTM.SharedKernel.Events;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;
using UTM.TelemetryService.Pipeline;
using UTM.TelemetryService.Services;
using UTM.TelemetryService.Simulation;

namespace UTM.TelemetryService;

/// <summary>
/// 遥测数据处理后台服务
/// 协调整个遥测数据处理流程
/// </summary>
public class TelemetryWorker : BackgroundService
{
    private readonly ILogger<TelemetryWorker> _logger;
    private readonly ITelemetryPipeline _pipeline;
    private readonly IDroneStateStore _stateStore;
    private readonly ITrajectoryService _trajectoryService;
    private readonly IEventBus _eventBus;
    private readonly DroneSimulator _simulator;
    private readonly IConfiguration _configuration;

    private int _simulatedDroneCount;
    private double _simulationIntervalMs;
    private double _statsIntervalSeconds = 5.0;

    public TelemetryWorker(
        ILogger<TelemetryWorker> logger,
        ITelemetryPipeline pipeline,
        IDroneStateStore stateStore,
        ITrajectoryService trajectoryService,
        IEventBus eventBus,
        DroneSimulator simulator,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _trajectoryService = trajectoryService ?? throw new ArgumentNullException(nameof(trajectoryService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _simulatedDroneCount = _configuration.GetValue("Simulation:DroneCount", 100);
        _simulationIntervalMs = _configuration.GetValue("Simulation:IntervalMs", 100.0);
        _statsIntervalSeconds = _configuration.GetValue("Simulation:StatsIntervalSeconds", 5.0);

        InitializeSimulator();

        _logger.LogInformation(
            "Telemetry service started with {DroneCount} simulated drones, interval {Interval}ms",
            _simulatedDroneCount, _simulationIntervalMs);

        var processingTask = ProcessTelemetryDataAsync(stoppingToken);
        var simulationTask = RunSimulationAsync(stoppingToken);
        var statsTask = ReportStatsAsync(stoppingToken);

        await Task.WhenAll(processingTask, simulationTask, statsTask);
    }

    private void InitializeSimulator()
    {
        double centerLon = _configuration.GetValue("Simulation:CenterLongitude", 116.3972);
        double centerLat = _configuration.GetValue("Simulation:CenterLatitude", 39.9075);
        double baseAlt = _configuration.GetValue("Simulation:BaseAltitude", 100.0);

        _simulator.AddDrones(_simulatedDroneCount, centerLon, centerLat, baseAlt);
        _logger.LogInformation("Simulator initialized with {Count} drones", _simulatedDroneCount);
    }

    /// <summary>
    /// 运行模拟数据生成
    /// </summary>
    private async Task RunSimulationAsync(CancellationToken stoppingToken)
    {
        double deltaTime = _simulationIntervalMs / 1000.0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var telemetryList = _simulator.Tick(deltaTime);
                await _pipeline.WriteBatchAsync(telemetryList, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simulation loop");
            }

            await Task.Delay((int)_simulationIntervalMs, stoppingToken);
        }

        _logger.LogInformation("Simulation loop stopped");
    }

    /// <summary>
    /// 处理遥测数据管道
    /// </summary>
    private async Task ProcessTelemetryDataAsync(CancellationToken stoppingToken)
    {
        int processedCount = 0;

        await foreach (var telemetry in _pipeline.ReadAllAsync(stoppingToken))
        {
            try
            {
                _stateStore.UpdateDrone(telemetry);
                _trajectoryService.AddTrajectoryPoint(telemetry);

                await _eventBus.PublishAsync(new TelemetryReceivedEvent
                {
                    Telemetry = telemetry
                }, stoppingToken);

                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing telemetry for drone {DroneId}", telemetry.DroneId);
            }
        }

        _logger.LogInformation("Telemetry processing stopped, processed {Count} messages", processedCount);
    }

    /// <summary>
    /// 定期输出统计信息
    /// </summary>
    private async Task ReportStatsAsync(CancellationToken stoppingToken)
    {
        long lastProcessed = 0;
        DateTime lastReport = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_statsIntervalSeconds), stoppingToken);

            try
            {
                long currentProcessed = _pipeline.TotalProcessed;
                long delta = currentProcessed - lastProcessed;
                double elapsed = (DateTime.UtcNow - lastReport).TotalSeconds;
                double throughput = delta / elapsed;

                _logger.LogInformation(
                    "Stats: Queue={QueueLength}, TotalProcessed={Total}, " +
                    "Throughput={Throughput:F0} msg/s, Drones={DroneCount}, " +
                    "TrackedTrajectories={TrajectoryCount}",
                    _pipeline.QueueLength,
                    currentProcessed,
                    throughput,
                    _stateStore.Count,
                    _trajectoryService.TrackedDroneCount);

                lastProcessed = currentProcessed;
                lastReport = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting stats");
            }
        }
    }
}
