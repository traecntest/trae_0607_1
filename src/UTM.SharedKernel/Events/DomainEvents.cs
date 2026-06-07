using UTM.SharedKernel.Models;

namespace UTM.SharedKernel.Events;

/// <summary>
/// 领域事件基接口
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// 事件唯一标识
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// 事件发生时间 (UTC)
    /// </summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// 事件类型名称
    /// </summary>
    string EventType { get; }
}

/// <summary>
/// 领域事件基类
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    public Guid EventId { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; protected set; } = DateTimeOffset.UtcNow;
    public abstract string EventType { get; }
}

/// <summary>
/// 遥测数据接收事件
/// 当遥测服务接收到新的无人机遥测数据时触发
/// </summary>
public class TelemetryReceivedEvent : DomainEventBase
{
    public override string EventType => "TelemetryReceived";

    /// <summary>
    /// 遥测数据
    /// </summary>
    public TelemetryData Telemetry { get; set; } = null!;
}

/// <summary>
/// 无人机状态变更事件
/// 当无人机飞行状态发生变化时触发
/// </summary>
public class DroneStatusChangedEvent : DomainEventBase
{
    public override string EventType => "DroneStatusChanged";

    /// <summary>
    /// 无人机ID
    /// </summary>
    public string DroneId { get; set; } = string.Empty;

    /// <summary>
    /// 原状态
    /// </summary>
    public DroneStatus OldStatus { get; set; }

    /// <summary>
    /// 新状态
    /// </summary>
    public DroneStatus NewStatus { get; set; }

    /// <summary>
    /// 变更时间
    /// </summary>
    public DateTimeOffset ChangedAt { get; set; }
}

/// <summary>
/// 冲突检测结果事件
/// 当检测到潜在冲突时触发
/// </summary>
public class ConflictDetectedEvent : DomainEventBase
{
    public override string EventType => "ConflictDetected";

    /// <summary>
    /// 冲突ID
    /// </summary>
    public string ConflictId { get; set; } = string.Empty;

    /// <summary>
    /// 涉事无人机ID列表
    /// </summary>
    public List<string> DroneIds { get; set; } = new();

    /// <summary>
    /// 冲突等级
    /// </summary>
    public ConflictSeverity Severity { get; set; }

    /// <summary>
    /// 预测碰撞时间 (秒，从现在开始)
    /// </summary>
    public double TimeToCollision { get; set; }

    /// <summary>
    /// 预测碰撞点
    /// </summary>
    public Position3D PredictedCollisionPoint { get; set; } = new();

    /// <summary>
    /// 最小预测距离 (m)
    /// </summary>
    public double MinimumDistance { get; set; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public DateTimeOffset DetectedAt { get; set; }
}

/// <summary>
/// 冲突严重程度
/// </summary>
public enum ConflictSeverity
{
    /// <summary>
    /// 低 - 预警级别，需要关注
    /// </summary>
    Low = 0,

    /// <summary>
    /// 中 - 可能发生冲突，建议采取行动
    /// </summary>
    Medium = 1,

    /// <summary>
    /// 高 - 高概率冲突，需要立即避让
    /// </summary>
    High = 2,

    /// <summary>
    /// 严重 - 即将碰撞，紧急避让
    /// </summary>
    Critical = 3
}

/// <summary>
/// 避让指令事件
/// 当需要向无人机发送避让指令时触发
/// </summary>
public class AvoidanceCommandEvent : DomainEventBase
{
    public override string EventType => "AvoidanceCommand";

    /// <summary>
    /// 目标无人机ID
    /// </summary>
    public string DroneId { get; set; } = string.Empty;

    /// <summary>
    /// 关联的冲突ID
    /// </summary>
    public string ConflictId { get; set; } = string.Empty;

    /// <summary>
    /// 避让指令类型
    /// </summary>
    public AvoidanceCommandType CommandType { get; set; }

    /// <summary>
    /// 目标位置（如果是机动避让）
    /// </summary>
    public Position3D? TargetPosition { get; set; }

    /// <summary>
    /// 目标速度
    /// </summary>
    public Velocity3D? TargetVelocity { get; set; }

    /// <summary>
    /// 等待时间 (秒) - 悬停等待
    /// </summary>
    public double WaitDuration { get; set; }

    /// <summary>
    /// 指令优先级
    /// </summary>
    public int Priority { get; set; } = 100;
}

/// <summary>
/// 避让指令类型
/// </summary>
public enum AvoidanceCommandType
{
    /// <summary>
    /// 悬停等待
    /// </summary>
    HoverAndWait = 0,

    /// <summary>
    /// 爬升避让
    /// </summary>
    Climb = 1,

    /// <summary>
    /// 下降避让
    /// </summary>
    Descend = 2,

    /// <summary>
    /// 左转
    /// </summary>
    TurnLeft = 3,

    /// <summary>
    /// 右转
    /// </summary>
    TurnRight = 4,

    /// <summary>
    /// 加速通过
    /// </summary>
    SpeedUp = 5,

    /// <summary>
    /// 减速让行
    /// </summary>
    SlowDown = 6,

    /// <summary>
    /// 机动到指定位置
    /// </summary>
    ManeuverToPosition = 7,

    /// <summary>
    /// 紧急返航
    /// </summary>
    ReturnToHome = 8
}

/// <summary>
/// 轨迹预测更新事件
/// </summary>
public class TrajectoryPredictedEvent : DomainEventBase
{
    public override string EventType => "TrajectoryPredicted";

    /// <summary>
    /// 无人机ID
    /// </summary>
    public string DroneId { get; set; } = string.Empty;

    /// <summary>
    /// 预测轨迹点
    /// </summary>
    public List<TrajectoryPoint> PredictedTrajectory { get; set; } = new();

    /// <summary>
    /// 预测时间跨度 (秒)
    /// </summary>
    public double PredictionHorizon { get; set; }

    /// <summary>
    /// 预测时间
    /// </summary>
    public DateTimeOffset PredictedAt { get; set; }
}

/// <summary>
/// 服务健康检查事件
/// </summary>
public class ServiceHeartbeatEvent : DomainEventBase
{
    public override string EventType => "ServiceHeartbeat";

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 服务实例ID
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 是否健康
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// CPU使用率 (%)
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// 内存使用量 (MB)
    /// </summary>
    public double MemoryUsage { get; set; }

    /// <summary>
    /// 消息队列长度
    /// </summary>
    public int QueueLength { get; set; }
}
