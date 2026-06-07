using UTM.ConflictDetectionService.Avoidance;
using UTM.ConflictDetectionService.Detection;
using UTM.SharedKernel.Events;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;

namespace UTM.ConflictDetectionService;

/// <summary>
/// 冲突检测与规避后台服务
/// 定期运行冲突检测算法，生成避让指令
/// </summary>
public class ConflictDetectionWorker : BackgroundService
{
    private readonly ILogger<ConflictDetectionWorker> _logger;
    private readonly IConflictDetector _conflictDetector;
    private readonly IAvoidancePlanner _avoidancePlanner;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;

    private readonly Dictionary<string, Drone> _droneStates = new();
    private readonly object _droneStatesLock = new();

    private TimeSpan _detectionInterval;
    private double _safetyDistance;
    private double _predictionHorizon;
    private double _statsIntervalSeconds = 5.0;

    public ConflictDetectionWorker(
        ILogger<ConflictDetectionWorker> logger,
        IConflictDetector conflictDetector,
        IAvoidancePlanner avoidancePlanner,
        IEventBus eventBus,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _conflictDetector = conflictDetector ?? throw new ArgumentNullException(nameof(conflictDetector));
        _avoidancePlanner = avoidancePlanner ?? throw new ArgumentNullException(nameof(avoidancePlanner));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _detectionInterval = TimeSpan.FromMilliseconds(
            _configuration.GetValue("Detection:IntervalMs", 200));
        _safetyDistance = _configuration.GetValue("Detection:SafetyDistanceMeters", 30.0);
        _predictionHorizon = _configuration.GetValue("Detection:PredictionHorizonSeconds", 60.0);
        _statsIntervalSeconds = _configuration.GetValue("Detection:StatsIntervalSeconds", 5.0);

        _conflictDetector.SafetyDistance = _safetyDistance;
        _conflictDetector.PredictionHorizon = _predictionHorizon;

        _logger.LogInformation(
            "Conflict detection service started. Interval={Interval}ms, SafetyDistance={Safety}m, Horizon={Horizon}s",
            _detectionInterval.TotalMilliseconds, _safetyDistance, _predictionHorizon);

        SubscribeToEvents();

        var detectionTask = RunDetectionLoopAsync(stoppingToken);
        var statsTask = ReportStatsAsync(stoppingToken);

        await Task.WhenAll(detectionTask, statsTask);
    }

    private void SubscribeToEvents()
    {
        _eventBus.Subscribe<TelemetryReceivedEvent>(OnTelemetryReceivedAsync);
    }

    private Task OnTelemetryReceivedAsync(TelemetryReceivedEvent evt)
    {
        UpdateDroneState(evt.Telemetry);
        return Task.CompletedTask;
    }

    private void UpdateDroneState(TelemetryData telemetry)
    {
        lock (_droneStatesLock)
        {
            if (!_droneStates.TryGetValue(telemetry.DroneId, out var drone))
            {
                drone = new Drone
                {
                    Id = telemetry.DroneId,
                    Name = telemetry.DroneId,
                    Type = DroneType.Multirotor,
                    MaxSpeed = 25.0,
                    MaxAltitude = 500.0,
                    SafetyRadius = 5.0
                };
                _droneStates[telemetry.DroneId] = drone;
            }

            drone.CurrentPosition = telemetry.Position;
            drone.Velocity = telemetry.Velocity;
            drone.Status = telemetry.Status;
            drone.BatteryLevel = telemetry.BatteryLevel;
            drone.LastUpdateTime = telemetry.Timestamp;
        }
    }

    /// <summary>
    /// 运行冲突检测循环
    /// </summary>
    private async Task RunDetectionLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                List<Drone> drones;
                lock (_droneStatesLock)
                {
                    drones = _droneStates.Values.ToList();
                }

                if (drones.Count >= 2)
                {
                    var conflicts = await _conflictDetector.DetectConflictsAsync(drones, stoppingToken);

                    foreach (var conflict in conflicts)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        var dronesDict = new Dictionary<string, Drone>();
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

            await Task.Delay(_detectionInterval, stoppingToken);
        }

        _logger.LogInformation("Conflict detection loop stopped");
    }

    /// <summary>
    /// 定期输出统计信息
    /// </summary>
    private async Task ReportStatsAsync(CancellationToken stoppingToken)
    {
        DateTime lastReport = DateTime.UtcNow;
        int lastConflictCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_statsIntervalSeconds), stoppingToken);

            try
            {
                int droneCount;
                lock (_droneStatesLock)
                {
                    droneCount = _droneStates.Count;
                }

                var activeConflicts = ((TimeWindowConflictDetector)_conflictDetector).GetActiveConflicts();
                int newConflicts = activeConflicts.Count - lastConflictCount;

                _logger.LogInformation(
                    "Stats: Drones={DroneCount}, ActiveConflicts={ActiveCount}, " +
                    "Critical={Critical}, High={High}, Medium={Medium}, Low={Low}",
                    droneCount,
                    activeConflicts.Count,
                    activeConflicts.Count(c => c.Severity == ConflictSeverity.Critical),
                    activeConflicts.Count(c => c.Severity == ConflictSeverity.High),
                    activeConflicts.Count(c => c.Severity == ConflictSeverity.Medium),
                    activeConflicts.Count(c => c.Severity == ConflictSeverity.Low));

                lastConflictCount = activeConflicts.Count;
                lastReport = DateTime.UtcNow;

                ((TimeWindowConflictDetector)_conflictDetector).CleanupExpiredConflicts(TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting stats");
            }
        }
    }
}
