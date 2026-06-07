using UTM.ConflictDetectionService.Avoidance;
using UTM.ConflictDetectionService.Detection;
using UTM.TelemetryService.Pipeline;
using UTM.TelemetryService.Services;
using UTM.TelemetryService.Simulation;

namespace UTM.ApiGateway.Services;

/// <summary>
/// 模拟数据生成托管服务
/// 生成模拟的无人机遥测数据
/// </summary>
public class SimulationHostedService : BackgroundService
{
    private readonly ILogger<SimulationHostedService> _logger;
    private readonly DroneSimulator _simulator;
    private readonly ITelemetryPipeline _pipeline;
    private readonly IConfiguration _configuration;

    public SimulationHostedService(
        ILogger<SimulationHostedService> logger,
        DroneSimulator simulator,
        ITelemetryPipeline pipeline,
        IConfiguration configuration)
    {
        _logger = logger;
        _simulator = simulator;
        _pipeline = pipeline;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int droneCount = _configuration.GetValue("Simulation:DroneCount", 100);
        int intervalMs = _configuration.GetValue("Simulation:IntervalMs", 100);
        double centerLon = _configuration.GetValue("Simulation:CenterLongitude", 116.3972);
        double centerLat = _configuration.GetValue("Simulation:CenterLatitude", 39.9075);
        double baseAlt = _configuration.GetValue("Simulation:BaseAltitude", 100.0);

        _simulator.AddDrones(droneCount, centerLon, centerLat, baseAlt);
        _logger.LogInformation("Simulation started with {Count} drones", droneCount);

        double deltaTime = intervalMs / 1000.0;

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

            await Task.Delay(intervalMs, stoppingToken);
        }

        _logger.LogInformation("Simulation stopped");
    }
}

/// <summary>
/// 遥测数据处理托管服务
/// 从管道读取并处理遥测数据
/// </summary>
public class TelemetryProcessingHostedService : BackgroundService
{
    private readonly ILogger<TelemetryProcessingHostedService> _logger;
    private readonly ITelemetryPipeline _pipeline;
    private readonly IDroneStateStore _stateStore;
    private readonly ITrajectoryService _trajectoryService;
    private readonly SharedKernel.Infrastructure.IEventBus _eventBus;
    private readonly IConfiguration _configuration;

    public TelemetryProcessingHostedService(
        ILogger<TelemetryProcessingHostedService> logger,
        ITelemetryPipeline pipeline,
        IDroneStateStore stateStore,
        ITrajectoryService trajectoryService,
        SharedKernel.Infrastructure.IEventBus eventBus,
        IConfiguration configuration)
    {
        _logger = logger;
        _pipeline = pipeline;
        _stateStore = stateStore;
        _trajectoryService = trajectoryService;
        _eventBus = eventBus;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry processing started");

        await foreach (var telemetry in _pipeline.ReadAllAsync(stoppingToken))
        {
            try
            {
                _stateStore.UpdateDrone(telemetry);
                _trajectoryService.AddTrajectoryPoint(telemetry);

                await _eventBus.PublishAsync(new SharedKernel.Events.TelemetryReceivedEvent
                {
                    Telemetry = telemetry
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing telemetry for drone {DroneId}", telemetry.DroneId);
            }
        }

        _logger.LogInformation("Telemetry processing stopped");
    }
}

/// <summary>
/// 冲突检测托管服务
/// 定期运行冲突检测算法
/// </summary>
public class ConflictDetectionHostedService : BackgroundService
{
    private readonly ILogger<ConflictDetectionHostedService> _logger;
    private readonly IDroneStateStore _stateStore;
    private readonly IConflictDetector _conflictDetector;
    private readonly IAvoidancePlanner _avoidancePlanner;
    private readonly IConfiguration _configuration;

    public ConflictDetectionHostedService(
        ILogger<ConflictDetectionHostedService> logger,
        IDroneStateStore stateStore,
        IConflictDetector conflictDetector,
        IAvoidancePlanner avoidancePlanner,
        IConfiguration configuration)
    {
        _logger = logger;
        _stateStore = stateStore;
        _conflictDetector = conflictDetector;
        _avoidancePlanner = avoidancePlanner;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int intervalMs = _configuration.GetValue("Detection:IntervalMs", 200);
        double safetyDistance = _configuration.GetValue("Detection:SafetyDistanceMeters", 30.0);
        double predictionHorizon = _configuration.GetValue("Detection:PredictionHorizonSeconds", 60.0);

        _conflictDetector.SafetyDistance = safetyDistance;
        _conflictDetector.PredictionHorizon = predictionHorizon;

        _logger.LogInformation(
            "Conflict detection started. Interval={Interval}ms, SafetyDistance={Safety}m",
            intervalMs, safetyDistance);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var drones = _stateStore.GetAllDrones().ToList();

                if (drones.Count >= 2)
                {
                    var conflicts = await _conflictDetector.DetectConflictsAsync(drones, stoppingToken);

                    foreach (var conflict in conflicts)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        var dronesDict = new Dictionary<string, SharedKernel.Models.Drone>();
                        foreach (var d in drones)
                            dronesDict[d.Id] = d;

                        await _avoidancePlanner.GenerateAvoidanceCommandsAsync(
                            conflict, dronesDict, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in conflict detection loop");
            }

            await Task.Delay(intervalMs, stoppingToken);
        }

        _logger.LogInformation("Conflict detection stopped");
    }
}
