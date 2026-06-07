namespace UTM.SharedKernel.Models;

/// <summary>
/// 无人机实体模型
/// </summary>
public class Drone
{
    /// <summary>
    /// 无人机唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 无人机名称/编号
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 无人机类型
    /// </summary>
    public DroneType Type { get; set; }

    /// <summary>
    /// 最大飞行速度 (m/s)
    /// </summary>
    public double MaxSpeed { get; set; }

    /// <summary>
    /// 最大飞行高度 (m)
    /// </summary>
    public double MaxAltitude { get; set; }

    /// <summary>
    /// 安全半径 (m)
    /// </summary>
    public double SafetyRadius { get; set; }

    /// <summary>
    /// 当前状态
    /// </summary>
    public DroneStatus Status { get; set; }

    /// <summary>
    /// 当前位置
    /// </summary>
    public Position3D CurrentPosition { get; set; } = new();

    /// <summary>
    /// 速度向量 (m/s)
    /// </summary>
    public Velocity3D Velocity { get; set; } = new();

    /// <summary>
    /// 电池电量百分比
    /// </summary>
    public double BatteryLevel { get; set; } = 100.0;

    /// <summary>
    /// 最后更新时间戳
    /// </summary>
    public DateTimeOffset LastUpdateTime { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 无人机类型枚举
/// </summary>
public enum DroneType
{
    /// <summary>
    /// 多旋翼
    /// </summary>
    Multirotor = 0,

    /// <summary>
    /// 固定翼
    /// </summary>
    FixedWing = 1,

    /// <summary>
    /// 垂直起降
    /// </summary>
    VTOL = 2,

    /// <summary>
    /// 直升机
    /// </summary>
    Helicopter = 3
}

/// <summary>
/// 无人机状态枚举
/// </summary>
public enum DroneStatus
{
    /// <summary>
    /// 未知
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 空闲/地面待命
    /// </summary>
    Idle = 1,

    /// <summary>
    /// 起飞中
    /// </summary>
    TakingOff = 2,

    /// <summary>
    /// 飞行中
    /// </summary>
    Flying = 3,

    /// <summary>
    /// 悬停
    /// </summary>
    Hovering = 4,

    /// <summary>
    /// 降落中
    /// </summary>
    Landing = 5,

    /// <summary>
    /// 已着陆
    /// </summary>
    Landed = 6,

    /// <summary>
    /// 紧急状态
    /// </summary>
    Emergency = 7,

    /// <summary>
    /// 失联
    /// </summary>
    Lost = 8
}
