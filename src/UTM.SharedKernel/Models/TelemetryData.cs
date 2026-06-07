namespace UTM.SharedKernel.Models;

/// <summary>
/// 无人机遥测数据包
/// 高频传输的状态数据
/// </summary>
public class TelemetryData
{
    /// <summary>
    /// 无人机ID
    /// </summary>
    public string DroneId { get; set; } = string.Empty;

    /// <summary>
    /// 数据包序号
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// 数据采集时间戳 (UTC)
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 当前位置
    /// </summary>
    public Position3D Position { get; set; } = new();

    /// <summary>
    /// 当前速度
    /// </summary>
    public Velocity3D Velocity { get; set; } = new();

    /// <summary>
    /// 加速度 (m/s²)
    /// </summary>
    public double Acceleration { get; set; }

    /// <summary>
    /// 航向角 (度)
    /// </summary>
    public double Heading { get; set; }

    /// <summary>
    /// 俯仰角 (度)
    /// </summary>
    public double Pitch { get; set; }

    /// <summary>
    /// 横滚角 (度)
    /// </summary>
    public double Roll { get; set; }

    /// <summary>
    /// 电池电量百分比
    /// </summary>
    public double BatteryLevel { get; set; } = 100.0;

    /// <summary>
    /// 电池电压 (V)
    /// </summary>
    public double BatteryVoltage { get; set; }

    /// <summary>
    /// 当前飞行状态
    /// </summary>
    public DroneStatus Status { get; set; }

    /// <summary>
    /// GPS卫星数
    /// </summary>
    public int GpsSatellites { get; set; }

    /// <summary>
    /// GPS定位精度 (m)
    /// </summary>
    public double GpsAccuracy { get; set; }

    /// <summary>
    /// 信号强度 (0-100%)
    /// </summary>
    public double SignalStrength { get; set; } = 100.0;

    /// <summary>
    /// 飞行模式
    /// </summary>
    public string FlightMode { get; set; } = "MANUAL";
}

/// <summary>
/// 轨迹点 - 用于轨迹记录和预测
/// </summary>
public struct TrajectoryPoint
{
    /// <summary>
    /// 时间偏移 (秒，相对于轨迹起点)
    /// </summary>
    public double TimeOffset { get; set; }

    /// <summary>
    /// 位置
    /// </summary>
    public Position3D Position { get; set; }

    /// <summary>
    /// 速度
    /// </summary>
    public Velocity3D Velocity { get; set; }

    public TrajectoryPoint() { }

    public TrajectoryPoint(double timeOffset, Position3D position, Velocity3D velocity)
    {
        TimeOffset = timeOffset;
        Position = position;
        Velocity = velocity;
    }
}

/// <summary>
/// 三维包围盒
/// 用于碰撞检测
/// </summary>
public struct BoundingBox3D
{
    /// <summary>
    /// 最小点
    /// </summary>
    public Position3D Min { get; set; }

    /// <summary>
    /// 最大点
    /// </summary>
    public Position3D Max { get; set; }

    public BoundingBox3D() { }

    public BoundingBox3D(Position3D min, Position3D max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// 中心点
    /// </summary>
    public Position3D Center => new(
        (Min.Longitude + Max.Longitude) / 2,
        (Min.Latitude + Max.Latitude) / 2,
        (Min.Altitude + Max.Altitude) / 2
    );

    /// <summary>
    /// 宽度 (经度方向，米)
    /// </summary>
    public double Width => Max.Longitude - Min.Longitude;

    /// <summary>
    /// 高度 (纬度方向，米)
    /// </summary>
    public double Height => Max.Latitude - Min.Latitude;

    /// <summary>
    /// 深度 (高度方向，米)
    /// </summary>
    public double Depth => Max.Altitude - Min.Altitude;

    /// <summary>
    /// 检测是否与另一个包围盒相交
    /// </summary>
    public bool Intersects(BoundingBox3D other)
    {
        return Min.Longitude <= other.Max.Longitude &&
               Max.Longitude >= other.Min.Longitude &&
               Min.Latitude <= other.Max.Latitude &&
               Max.Latitude >= other.Min.Latitude &&
               Min.Altitude <= other.Max.Altitude &&
               Max.Altitude >= other.Min.Altitude;
    }

    /// <summary>
    /// 检测点是否在包围盒内
    /// </summary>
    public bool Contains(Position3D point)
    {
        return point.Longitude >= Min.Longitude &&
               point.Longitude <= Max.Longitude &&
               point.Latitude >= Min.Latitude &&
               point.Latitude <= Max.Latitude &&
               point.Altitude >= Min.Altitude &&
               point.Altitude <= Max.Altitude;
    }
}

/// <summary>
/// 飞行计划/航线
/// </summary>
public class FlightPlan
{
    /// <summary>
    /// 飞行计划ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 无人机ID
    /// </summary>
    public string DroneId { get; set; } = string.Empty;

    /// <summary>
    /// 计划起飞时间
    /// </summary>
    public DateTimeOffset DepartureTime { get; set; }

    /// <summary>
    /// 计划降落时间
    /// </summary>
    public DateTimeOffset ArrivalTime { get; set; }

    /// <summary>
    /// 航路点列表
    /// </summary>
    public List<Position3D> Waypoints { get; set; } = new();

    /// <summary>
    /// 巡航速度 (m/s)
    /// </summary>
    public double CruiseSpeed { get; set; } = 15.0;

    /// <summary>
    /// 巡航高度 (m)
    /// </summary>
    public double CruiseAltitude { get; set; } = 100.0;
}
