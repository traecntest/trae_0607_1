using System.Numerics;

namespace UTM.SharedKernel.Models;

/// <summary>
/// 三维位置坐标 (WGS84 + 海拔)
/// </summary>
public struct Position3D : IEquatable<Position3D>
{
    /// <summary>
    /// 经度 (度)
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// 纬度 (度)
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// 海拔高度 (米，相对于海平面)
    /// </summary>
    public double Altitude { get; set; }

    public Position3D() { }

    public Position3D(double longitude, double latitude, double altitude)
    {
        Longitude = longitude;
        Latitude = latitude;
        Altitude = altitude;
    }

    /// <summary>
    /// 转换为三维向量 (米为单位，使用近似投影)
    /// </summary>
    public Vector3 ToVector3()
    {
        const double EarthRadius = 6371000.0;
        double latRad = Latitude * Math.PI / 180.0;
        double lonRad = Longitude * Math.PI / 180.0;

        double x = EarthRadius * Math.Cos(latRad) * Math.Cos(lonRad);
        double y = EarthRadius * Math.Cos(latRad) * Math.Sin(lonRad);
        double z = Altitude;

        return new Vector3((float)x, (float)y, (float)z);
    }

    public bool Equals(Position3D other)
    {
        return Longitude.Equals(other.Longitude) &&
               Latitude.Equals(other.Latitude) &&
               Altitude.Equals(other.Altitude);
    }

    public override bool Equals(object? obj)
    {
        return obj is Position3D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Longitude, Latitude, Altitude);
    }

    public static bool operator ==(Position3D left, Position3D right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Position3D left, Position3D right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"Lon:{Longitude:F6}, Lat:{Latitude:F6}, Alt:{Altitude:F2}m";
    }
}

/// <summary>
/// 三维速度向量
/// </summary>
public struct Velocity3D : IEquatable<Velocity3D>
{
    /// <summary>
    /// 东向速度 (m/s)
    /// </summary>
    public double East { get; set; }

    /// <summary>
    /// 北向速度 (m/s)
    /// </summary>
    public double North { get; set; }

    /// <summary>
    /// 垂直速度 (m/s，向上为正)
    /// </summary>
    public double Up { get; set; }

    public Velocity3D() { }

    public Velocity3D(double east, double north, double up)
    {
        East = east;
        North = north;
        Up = up;
    }

    /// <summary>
    /// 速度大小 (m/s)
    /// </summary>
    public double Speed => Math.Sqrt(East * East + North * North + Up * Up);

    /// <summary>
    /// 水平速度大小 (m/s)
    /// </summary>
    public double HorizontalSpeed => Math.Sqrt(East * East + North * North);

    /// <summary>
    /// 航向角 (度，从正北顺时针)
    /// </summary>
    public double Heading
    {
        get
        {
            double heading = Math.Atan2(East, North) * 180.0 / Math.PI;
            return heading < 0 ? heading + 360.0 : heading;
        }
    }

    public Vector3 ToVector3()
    {
        return new Vector3((float)East, (float)North, (float)Up);
    }

    public bool Equals(Velocity3D other)
    {
        return East.Equals(other.East) &&
               North.Equals(other.North) &&
               Up.Equals(other.Up);
    }

    public override bool Equals(object? obj)
    {
        return obj is Velocity3D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(East, North, Up);
    }

    public static bool operator ==(Velocity3D left, Velocity3D right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Velocity3D left, Velocity3D right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"E:{East:F2}, N:{North:F2}, U:{Up:F2} m/s (Speed:{Speed:F2} m/s)";
    }
}
