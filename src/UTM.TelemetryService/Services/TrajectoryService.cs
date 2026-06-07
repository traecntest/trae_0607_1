using System.Collections.Concurrent;
using UTM.SharedKernel.Events;
using UTM.SharedKernel.Geometry;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;

namespace UTM.TelemetryService.Services;

/// <summary>
/// 轨迹跟踪服务
/// 维护无人机的历史轨迹，提供轨迹查询和预测功能
/// </summary>
public interface ITrajectoryService
{
    /// <summary>
    /// 添加轨迹点
    /// </summary>
    void AddTrajectoryPoint(TelemetryData telemetry);

    /// <summary>
    /// 获取无人机历史轨迹
    /// </summary>
    IReadOnlyList<TrajectoryPoint> GetTrajectory(string droneId, TimeSpan timeRange);

    /// <summary>
    /// 预测无人机未来轨迹
    /// </summary>
    List<TrajectoryPoint> PredictTrajectory(string droneId, double predictionHorizonSeconds, double timeStep = 1.0);

    /// <summary>
    /// 获取跟踪的无人机数量
    /// </summary>
    int TrackedDroneCount { get; }
}

/// <summary>
/// 基于滑动时间窗的轨迹跟踪服务实现
/// </summary>
public class TrajectoryService : ITrajectoryService
{
    private readonly ConcurrentDictionary<string, SlidingTimeWindow<TrajectoryRecord>> _trajectories = new();
    private readonly IDroneStateStore _droneStateStore;
    private readonly IEventBus _eventBus;
    private readonly TimeSpan _trajectoryRetention;

    public int TrackedDroneCount => _trajectories.Count;

    public TrajectoryService(
        IDroneStateStore droneStateStore,
        IEventBus eventBus,
        TimeSpan? trajectoryRetention = null)
    {
        _droneStateStore = droneStateStore ?? throw new ArgumentNullException(nameof(droneStateStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _trajectoryRetention = trajectoryRetention ?? TimeSpan.FromMinutes(5);
    }

    public void AddTrajectoryPoint(TelemetryData telemetry)
    {
        if (telemetry == null)
            throw new ArgumentNullException(nameof(telemetry));

        var record = new TrajectoryRecord
        {
            Timestamp = telemetry.Timestamp,
            Position = telemetry.Position,
            Velocity = telemetry.Velocity
        };

        var window = _trajectories.GetOrAdd(
            telemetry.DroneId,
            _ => new SlidingTimeWindow<TrajectoryRecord>(_trajectoryRetention, r => r.Timestamp)
        );

        window.Add(record);
    }

    public IReadOnlyList<TrajectoryPoint> GetTrajectory(string droneId, TimeSpan timeRange)
    {
        if (!_trajectories.TryGetValue(droneId, out var window))
            return Array.Empty<TrajectoryPoint>();

        var startTime = DateTimeOffset.UtcNow - timeRange;
        var records = window.GetItemsInRange(startTime, DateTimeOffset.UtcNow);

        var points = new List<TrajectoryPoint>(records.Count);
        if (records.Count == 0)
            return points;

        var baseTime = records[0].Timestamp;
        foreach (var record in records)
        {
            points.Add(new TrajectoryPoint(
                (record.Timestamp - baseTime).TotalSeconds,
                record.Position,
                record.Velocity
            ));
        }

        return points;
    }

    public List<TrajectoryPoint> PredictTrajectory(string droneId, double predictionHorizonSeconds, double timeStep = 1.0)
    {
        var drone = _droneStateStore.GetDrone(droneId);
        if (drone == null)
            return new List<TrajectoryPoint>();

        var predicted = GeometryCalculator.GeneratePredictedTrajectory(
            drone.CurrentPosition,
            drone.Velocity,
            predictionHorizonSeconds,
            timeStep
        );

        _ = _eventBus.PublishAsync(new TrajectoryPredictedEvent
        {
            DroneId = droneId,
            PredictedTrajectory = predicted,
            PredictionHorizon = predictionHorizonSeconds,
            PredictedAt = DateTimeOffset.UtcNow
        });

        return predicted;
    }

    private class TrajectoryRecord : ITimestamped
    {
        public DateTimeOffset Timestamp { get; set; }
        public Position3D Position { get; set; } = new();
        public Velocity3D Velocity { get; set; } = new();
    }
}
