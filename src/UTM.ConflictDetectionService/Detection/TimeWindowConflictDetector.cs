using System.Collections.Concurrent;
using UTM.SharedKernel.Events;
using UTM.SharedKernel.Geometry;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;

namespace UTM.ConflictDetectionService.Detection;

/// <summary>
/// 冲突检测服务接口
/// </summary>
public interface IConflictDetector
{
    /// <summary>
    /// 检测所有无人机之间的潜在冲突
    /// </summary>
    Task<List<ConflictDetectedEvent>> DetectConflictsAsync(
        IEnumerable<Drone> drones,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检测单架无人机与其他无人机的冲突
    /// </summary>
    Task<List<ConflictDetectedEvent>> DetectDroneConflictsAsync(
        Drone drone,
        IEnumerable<Drone> otherDrones,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 安全距离阈值 (米)
    /// </summary>
    double SafetyDistance { get; set; }

    /// <summary>
    /// 预测时间范围 (秒)
    /// </summary>
    double PredictionHorizon { get; set; }
}

/// <summary>
/// 基于时间窗滑动检测的冲突检测器
/// 使用TCPA/DCPA算法预测潜在冲突
/// </summary>
public class TimeWindowConflictDetector : IConflictDetector
{
    private readonly IEventBus _eventBus;
    private readonly ConcurrentDictionary<string, ConflictDetectedEvent> _activeConflicts = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public double SafetyDistance { get; set; } = 30.0;

    /// <inheritdoc />
    public double PredictionHorizon { get; set; } = 60.0;

    public TimeWindowConflictDetector(IEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    public async Task<List<ConflictDetectedEvent>> DetectConflictsAsync(
        IEnumerable<Drone> drones,
        CancellationToken cancellationToken = default)
    {
        var droneList = drones.ToList();
        var conflicts = new List<ConflictDetectedEvent>();

        if (droneList.Count < 2)
            return conflicts;

        await Task.Run(() =>
        {
            for (int i = 0; i < droneList.Count; i++)
            {
                for (int j = i + 1; j < droneList.Count; j++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var conflict = DetectPairConflict(droneList[i], droneList[j]);
                    if (conflict != null)
                    {
                        conflicts.Add(conflict);
                        ProcessConflict(conflict);
                    }
                }
            }
        }, cancellationToken);

        return conflicts;
    }

    /// <inheritdoc />
    public async Task<List<ConflictDetectedEvent>> DetectDroneConflictsAsync(
        Drone drone,
        IEnumerable<Drone> otherDrones,
        CancellationToken cancellationToken = default)
    {
        var conflicts = new List<ConflictDetectedEvent>();

        await Task.Run(() =>
        {
            foreach (var other in otherDrones)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (other.Id == drone.Id)
                    continue;

                var conflict = DetectPairConflict(drone, other);
                if (conflict != null)
                {
                    conflicts.Add(conflict);
                    ProcessConflict(conflict);
                }
            }
        }, cancellationToken);

        return conflicts;
    }

    /// <summary>
    /// 检测两架无人机之间的冲突
    /// </summary>
    private ConflictDetectedEvent? DetectPairConflict(Drone drone1, Drone drone2)
    {
        if (drone1.Status == DroneStatus.Landed || drone1.Status == DroneStatus.Idle ||
            drone2.Status == DroneStatus.Landed || drone2.Status == DroneStatus.Idle)
            return null;

        double currentDistance = GeometryCalculator.Distance(
            drone1.CurrentPosition, drone2.CurrentPosition);

        double combinedSafetyRadius = drone1.SafetyRadius + drone2.SafetyRadius + SafetyDistance;

        if (currentDistance <= combinedSafetyRadius)
        {
            return CreateConflictEvent(drone1, drone2, 0, currentDistance, ConflictSeverity.Critical);
        }

        double tcpa = GeometryCalculator.CalculateTCPA(
            drone1.CurrentPosition, drone1.Velocity,
            drone2.CurrentPosition, drone2.Velocity);

        if (tcpa < 0 || tcpa > PredictionHorizon)
            return null;

        double dcpa = GeometryCalculator.CalculateDCPA(
            drone1.CurrentPosition, drone1.Velocity,
            drone2.CurrentPosition, drone2.Velocity, tcpa);

        if (dcpa > combinedSafetyRadius)
            return null;

        var severity = CalculateSeverity(tcpa, dcpa, combinedSafetyRadius);

        var predictedPoint = GeometryCalculator.PredictPosition(
            drone1.CurrentPosition, drone1.Velocity, tcpa);

        return CreateConflictEvent(drone1, drone2, tcpa, dcpa, severity, predictedPoint);
    }

    /// <summary>
    /// 计算冲突严重程度
    /// </summary>
    private static ConflictSeverity CalculateSeverity(
        double tcpa, double dcpa, double safetyDistance)
    {
        double timeRatio = tcpa / 30.0;
        double distanceRatio = dcpa / safetyDistance;

        if (tcpa < 5 || dcpa < safetyDistance * 0.3)
            return ConflictSeverity.Critical;

        if (tcpa < 15 || dcpa < safetyDistance * 0.6)
            return ConflictSeverity.High;

        if (tcpa < 30 || dcpa < safetyDistance * 0.9)
            return ConflictSeverity.Medium;

        return ConflictSeverity.Low;
    }

    private static ConflictDetectedEvent CreateConflictEvent(
        Drone drone1, Drone drone2,
        double timeToCollision, double minimumDistance,
        ConflictSeverity severity, Position3D? collisionPoint = null)
    {
        string conflictId = $"CONFLICT-{drone1.Id}-{drone2.Id}";

        return new ConflictDetectedEvent
        {
            ConflictId = conflictId,
            DroneIds = new List<string> { drone1.Id, drone2.Id },
            Severity = severity,
            TimeToCollision = timeToCollision,
            PredictedCollisionPoint = collisionPoint ?? new Position3D(
                (drone1.CurrentPosition.Longitude + drone2.CurrentPosition.Longitude) / 2,
                (drone1.CurrentPosition.Latitude + drone2.CurrentPosition.Latitude) / 2,
                (drone1.CurrentPosition.Altitude + drone2.CurrentPosition.Altitude) / 2
            ),
            MinimumDistance = minimumDistance,
            DetectedAt = DateTimeOffset.UtcNow
        };
    }

    private void ProcessConflict(ConflictDetectedEvent conflict)
    {
        bool isNew = true;
        ConflictDetectedEvent? existing = null;

        lock (_lock)
        {
            if (_activeConflicts.TryGetValue(conflict.ConflictId, out existing))
            {
                isNew = false;
                _activeConflicts[conflict.ConflictId] = conflict;
            }
            else
            {
                _activeConflicts[conflict.ConflictId] = conflict;
            }
        }

        if (isNew || existing?.Severity != conflict.Severity)
        {
            _ = _eventBus.PublishAsync(conflict);
        }
    }

    /// <summary>
    /// 获取当前活跃冲突
    /// </summary>
    public IReadOnlyCollection<ConflictDetectedEvent> GetActiveConflicts()
    {
        return _activeConflicts.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 清理过期冲突
    /// </summary>
    public void CleanupExpiredConflicts(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        var expiredKeys = _activeConflicts
            .Where(kvp => kvp.Value.DetectedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _activeConflicts.TryRemove(key, out _);
        }
    }
}
