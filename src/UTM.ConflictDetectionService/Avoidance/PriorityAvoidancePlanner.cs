using UTM.SharedKernel.Events;
using UTM.SharedKernel.Geometry;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;

namespace UTM.ConflictDetectionService.Avoidance;

/// <summary>
/// 避让决策服务接口
/// </summary>
public interface IAvoidancePlanner
{
    /// <summary>
    /// 生成避让指令
    /// </summary>
    Task<List<AvoidanceCommandEvent>> GenerateAvoidanceCommandsAsync(
        ConflictDetectedEvent conflict,
        IReadOnlyDictionary<string, Drone> drones,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于优先级的避让决策器
/// 根据冲突严重程度和无人机状态生成合适的避让指令
/// </summary>
public class PriorityAvoidancePlanner : IAvoidancePlanner
{
    private readonly IEventBus _eventBus;

    public PriorityAvoidancePlanner(IEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    public async Task<List<AvoidanceCommandEvent>> GenerateAvoidanceCommandsAsync(
        ConflictDetectedEvent conflict,
        IReadOnlyDictionary<string, Drone> drones,
        CancellationToken cancellationToken = default)
    {
        var commands = new List<AvoidanceCommandEvent>();

        if (conflict.DroneIds.Count < 2)
            return commands;

        await Task.Run(() =>
        {
            var drone1 = drones.GetValueOrDefault(conflict.DroneIds[0]);
            var drone2 = drones.GetValueOrDefault(conflict.DroneIds[1]);

            if (drone1 == null || drone2 == null)
                return;

            var (primaryDrone, secondaryDrone) = DeterminePriority(drone1, drone2);

            var primaryCommand = GenerateAvoidanceCommand(
                primaryDrone, secondaryDrone, conflict, true);
            if (primaryCommand != null)
            {
                commands.Add(primaryCommand);
                _ = _eventBus.PublishAsync(primaryCommand, cancellationToken);
            }

            if (conflict.Severity >= ConflictSeverity.High)
            {
                var secondaryCommand = GenerateAvoidanceCommand(
                    secondaryDrone, primaryDrone, conflict, false);
                if (secondaryCommand != null)
                {
                    commands.Add(secondaryCommand);
                    _ = _eventBus.PublishAsync(secondaryCommand, cancellationToken);
                }
            }
        }, cancellationToken);

        return commands;
    }

    /// <summary>
    /// 确定避让优先级
    /// 返回(需要避让的无人机, 保持航线的无人机)
    /// </summary>
    private static (Drone yielding, Drone keeping) DeterminePriority(Drone drone1, Drone drone2)
    {
        int priority1 = GetPriorityScore(drone1);
        int priority2 = GetPriorityScore(drone2);

        if (priority1 <= priority2)
            return (drone1, drone2);
        else
            return (drone2, drone1);
    }

    private static int GetPriorityScore(Drone drone)
    {
        int score = 0;

        if (drone.Status == DroneStatus.Emergency)
            score += 100;

        if (drone.BatteryLevel < 20)
            score += 50;

        if (drone.Type == DroneType.FixedWing)
            score += 30;

        return score;
    }

    /// <summary>
    /// 生成单架无人机的避让指令
    /// </summary>
    private static AvoidanceCommandEvent? GenerateAvoidanceCommand(
        Drone drone, Drone otherDrone,
        ConflictDetectedEvent conflict,
        bool isPrimary)
    {
        var commandType = SelectAvoidanceStrategy(drone, otherDrone, conflict, isPrimary);

        var command = new AvoidanceCommandEvent
        {
            DroneId = drone.Id,
            ConflictId = conflict.ConflictId,
            CommandType = commandType,
            Priority = isPrimary ? 100 : 80,
            WaitDuration = CalculateWaitDuration(conflict)
        };

        if (commandType == AvoidanceCommandType.ManeuverToPosition)
        {
            command.TargetPosition = CalculateAvoidPosition(drone, otherDrone, conflict);
        }

        if (commandType == AvoidanceCommandType.SpeedUp ||
            commandType == AvoidanceCommandType.SlowDown)
        {
            command.TargetVelocity = CalculateAdjustedVelocity(drone, otherDrone, conflict, commandType);
        }

        return command;
    }

    /// <summary>
    /// 选择避让策略
    /// </summary>
    private static AvoidanceCommandType SelectAvoidanceStrategy(
        Drone drone, Drone otherDrone,
        ConflictDetectedEvent conflict,
        bool isPrimary)
    {
        double relativeVertical = drone.CurrentPosition.Altitude - otherDrone.CurrentPosition.Altitude;
        double verticalSpeed = drone.Velocity.Up - otherDrone.Velocity.Up;

        switch (conflict.Severity)
        {
            case ConflictSeverity.Critical:
                if (Math.Abs(relativeVertical) < 50)
                    return AvoidanceCommandType.ManeuverToPosition;
                return relativeVertical > 0
                    ? AvoidanceCommandType.Climb
                    : AvoidanceCommandType.Descend;

            case ConflictSeverity.High:
                if (isPrimary)
                {
                    if (Math.Abs(verticalSpeed) > 2)
                        return relativeVertical > 0
                            ? AvoidanceCommandType.Climb
                            : AvoidanceCommandType.Descend;
                    return AvoidanceCommandType.SlowDown;
                }
                return AvoidanceCommandType.HoverAndWait;

            case ConflictSeverity.Medium:
                return AvoidanceCommandType.SlowDown;

            case ConflictSeverity.Low:
            default:
                return AvoidanceCommandType.HoverAndWait;
        }
    }

    /// <summary>
    /// 计算等待时间
    /// </summary>
    private static double CalculateWaitDuration(ConflictDetectedEvent conflict)
    {
        return conflict.TimeToCollision + 10.0;
    }

    /// <summary>
    /// 计算避让目标位置
    /// </summary>
    private static Position3D CalculateAvoidPosition(
        Drone drone, Drone otherDrone, ConflictDetectedEvent conflict)
    {
        var midPoint = new Position3D(
            (drone.CurrentPosition.Longitude + otherDrone.CurrentPosition.Longitude) / 2,
            (drone.CurrentPosition.Latitude + otherDrone.CurrentPosition.Latitude) / 2,
            (drone.CurrentPosition.Altitude + otherDrone.CurrentPosition.Altitude) / 2
        );

        double safeDistance = 50.0;
        double angle = Math.Atan2(
            drone.CurrentPosition.Latitude - otherDrone.CurrentPosition.Latitude,
            drone.CurrentPosition.Longitude - otherDrone.CurrentPosition.Longitude
        );

        double offsetLon = Math.Cos(angle) * safeDistance / 111000.0;
        double offsetLat = Math.Sin(angle) * safeDistance / 111000.0;

        return new Position3D(
            drone.CurrentPosition.Longitude + offsetLon,
            drone.CurrentPosition.Latitude + offsetLat,
            drone.CurrentPosition.Altitude
        );
    }

    /// <summary>
    /// 计算调整后的速度
    /// </summary>
    private static Velocity3D CalculateAdjustedVelocity(
        Drone drone, Drone otherDrone,
        ConflictDetectedEvent conflict,
        AvoidanceCommandType commandType)
    {
        double factor = commandType switch
        {
            AvoidanceCommandType.SpeedUp => 1.3,
            AvoidanceCommandType.SlowDown => 0.6,
            _ => 1.0
        };

        return new Velocity3D(
            drone.Velocity.East * factor,
            drone.Velocity.North * factor,
            drone.Velocity.Up * factor
        );
    }
}
