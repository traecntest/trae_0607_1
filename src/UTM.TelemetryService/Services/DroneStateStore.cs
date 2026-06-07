using System.Collections.Concurrent;
using UTM.SharedKernel.Events;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;

namespace UTM.TelemetryService.Services;

/// <summary>
/// 无人机状态存储服务
/// 管理所有无人机的实时状态
/// </summary>
public interface IDroneStateStore
{
    /// <summary>
    /// 获取无人机数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 获取所有无人机
    /// </summary>
    IEnumerable<Drone> GetAllDrones();

    /// <summary>
    /// 获取指定无人机
    /// </summary>
    Drone? GetDrone(string droneId);

    /// <summary>
    /// 更新无人机状态
    /// </summary>
    void UpdateDrone(TelemetryData telemetry);

    /// <summary>
    /// 获取指定区域内的无人机
    /// </summary>
    IEnumerable<Drone> GetDronesInArea(double minLon, double minLat, double maxLon, double maxLat);
}

/// <summary>
/// 基于内存的无人机状态存储
/// </summary>
public class InMemoryDroneStateStore : IDroneStateStore
{
    private readonly ConcurrentDictionary<string, Drone> _drones = new();
    private readonly IEventBus _eventBus;

    public int Count => _drones.Count;

    public InMemoryDroneStateStore(IEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public IEnumerable<Drone> GetAllDrones()
    {
        return _drones.Values;
    }

    public Drone? GetDrone(string droneId)
    {
        _drones.TryGetValue(droneId, out var drone);
        return drone;
    }

    public void UpdateDrone(TelemetryData telemetry)
    {
        if (telemetry == null)
            throw new ArgumentNullException(nameof(telemetry));

        var oldStatus = DroneStatus.Unknown;
        bool isNewDrone = true;

        var drone = _drones.AddOrUpdate(
            telemetry.DroneId,
            _ =>
            {
                isNewDrone = true;
                return CreateNewDrone(telemetry);
            },
            (_, existing) =>
            {
                isNewDrone = false;
                oldStatus = existing.Status;
                UpdateExistingDrone(existing, telemetry);
                return existing;
            }
        );

        if (!isNewDrone && oldStatus != telemetry.Status)
        {
            _ = _eventBus.PublishAsync(new DroneStatusChangedEvent
            {
                DroneId = telemetry.DroneId,
                OldStatus = oldStatus,
                NewStatus = telemetry.Status,
                ChangedAt = telemetry.Timestamp
            });
        }
    }

    private static Drone CreateNewDrone(TelemetryData telemetry)
    {
        return new Drone
        {
            Id = telemetry.DroneId,
            Name = telemetry.DroneId,
            Type = DroneType.Multirotor,
            MaxSpeed = 25.0,
            MaxAltitude = 500.0,
            SafetyRadius = 5.0,
            Status = telemetry.Status,
            CurrentPosition = telemetry.Position,
            Velocity = telemetry.Velocity,
            BatteryLevel = telemetry.BatteryLevel,
            LastUpdateTime = telemetry.Timestamp
        };
    }

    private static void UpdateExistingDrone(Drone drone, TelemetryData telemetry)
    {
        drone.CurrentPosition = telemetry.Position;
        drone.Velocity = telemetry.Velocity;
        drone.Status = telemetry.Status;
        drone.BatteryLevel = telemetry.BatteryLevel;
        drone.LastUpdateTime = telemetry.Timestamp;
    }

    public IEnumerable<Drone> GetDronesInArea(double minLon, double minLat, double maxLon, double maxLat)
    {
        return _drones.Values.Where(d =>
            d.CurrentPosition.Longitude >= minLon &&
            d.CurrentPosition.Longitude <= maxLon &&
            d.CurrentPosition.Latitude >= minLat &&
            d.CurrentPosition.Latitude <= maxLat
        );
    }
}
