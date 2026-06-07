using System.Numerics;
using UTM.SharedKernel.Models;

namespace UTM.SharedKernel.Geometry;

/// <summary>
/// 三维几何计算工具类
/// 使用 System.Numerics.Vectors 向量化加速计算
/// 集成 .NET 8 的高性能数值计算能力
/// </summary>
public static class GeometryCalculator
{
    private const double EarthRadiusMeters = 6371000.0;
    private const double DegreesToRadians = Math.PI / 180.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;

    /// <summary>
    /// 计算两点之间的球面距离 (Haversine公式)
    /// </summary>
    /// <param name="p1">点1</param>
    /// <param name="p2">点2</param>
    /// <returns>距离 (米)</returns>
    public static double Distance(Position3D p1, Position3D p2)
    {
        double dLat = (p2.Latitude - p1.Latitude) * DegreesToRadians;
        double dLon = (p2.Longitude - p1.Longitude) * DegreesToRadians;

        double lat1Rad = p1.Latitude * DegreesToRadians;
        double lat2Rad = p2.Latitude * DegreesToRadians;

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2) *
                   Math.Cos(lat1Rad) * Math.Cos(lat2Rad);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double horizontalDistance = EarthRadiusMeters * c;

        double altitudeDiff = p2.Altitude - p1.Altitude;

        return Math.Sqrt(horizontalDistance * horizontalDistance + altitudeDiff * altitudeDiff);
    }

    /// <summary>
    /// 使用向量化加速的批量距离计算
    /// 计算一个点到多个点的欧几里得距离（局部平面近似，适用于小范围）
    /// 使用 Vector&lt;double&gt; 实现 SIMD 向量化
    /// </summary>
    /// <param name="origin">原点</param>
    /// <param name="points">目标点数组 (已转换为米制坐标: east, north, up)</param>
    /// <returns>距离数组 (米)</returns>
    public static double[] DistanceBatch(Vector3 origin, Vector3[] points)
    {
        if (points == null || points.Length == 0)
            return Array.Empty<double>();

        double[] distances = new double[points.Length];
        int vectorSize = Vector<float>.Count;

        if (Vector.IsHardwareAccelerated && points.Length >= vectorSize)
        {
            int i = 0;

            for (; i <= points.Length - vectorSize; i += vectorSize)
            {
                Vector<float> px = new Vector<float>(GetComponentArray(points, i, vectorSize, p => p.X));
                Vector<float> py = new Vector<float>(GetComponentArray(points, i, vectorSize, p => p.Y));
                Vector<float> pz = new Vector<float>(GetComponentArray(points, i, vectorSize, p => p.Z));

                Vector<float> dx = px - new Vector<float>(origin.X);
                Vector<float> dy = py - new Vector<float>(origin.Y);
                Vector<float> dz = pz - new Vector<float>(origin.Z);

                Vector<float> distSquared = dx * dx + dy * dy + dz * dz;
                Vector<float> dist = Vector.SquareRoot(distSquared);

                float[] temp = new float[vectorSize];
                dist.CopyTo(temp);

                for (int j = 0; j < vectorSize; j++)
                    distances[i + j] = temp[j];
            }

            for (; i < points.Length; i++)
            {
                float dx = points[i].X - origin.X;
                float dy = points[i].Y - origin.Y;
                float dz = points[i].Z - origin.Z;
                distances[i] = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }
        else
        {
            for (int i = 0; i < points.Length; i++)
            {
                float dx = points[i].X - origin.X;
                float dy = points[i].Y - origin.Y;
                float dz = points[i].Z - origin.Z;
                distances[i] = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        return distances;
    }

    private static float[] GetComponentArray(Vector3[] points, int startIndex, int count, Func<Vector3, float> selector)
    {
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = selector(points[startIndex + i]);
        }
        return result;
    }

    /// <summary>
    /// 将经纬度坐标转换为局部平面坐标 (米)
    /// 使用墨卡托近似，适用于小范围区域
    /// </summary>
    public static Vector3 ToLocalMeters(Position3D position, Position3D referencePoint)
    {
        double latRad = referencePoint.Latitude * DegreesToRadians;
        double cosLat = Math.Cos(latRad);

        float east = (float)((position.Longitude - referencePoint.Longitude) * DegreesToRadians * EarthRadiusMeters * cosLat);
        float north = (float)((position.Latitude - referencePoint.Latitude) * DegreesToRadians * EarthRadiusMeters);
        float up = (float)(position.Altitude - referencePoint.Altitude);

        return new Vector3(east, north, up);
    }

    /// <summary>
    /// 检测两个包围盒是否相交 (AABB碰撞检测)
    /// 使用 Vector 向量化加速
    /// </summary>
    public static bool BoundingBoxIntersects(BoundingBox3D a, BoundingBox3D b)
    {
        Vector3 aMin = new Vector3((float)a.Min.Longitude, (float)a.Min.Latitude, (float)a.Min.Altitude);
        Vector3 aMax = new Vector3((float)a.Max.Longitude, (float)a.Max.Latitude, (float)a.Max.Altitude);
        Vector3 bMin = new Vector3((float)b.Min.Longitude, (float)b.Min.Latitude, (float)b.Min.Altitude);
        Vector3 bMax = new Vector3((float)b.Max.Longitude, (float)b.Max.Latitude, (float)b.Max.Altitude);

        Vector3 overlapMin = Vector3.Max(aMin, bMin);
        Vector3 overlapMax = Vector3.Min(aMax, bMax);

        return overlapMin.X <= overlapMax.X &&
               overlapMin.Y <= overlapMax.Y &&
               overlapMin.Z <= overlapMax.Z;
    }

    /// <summary>
    /// 批量AABB碰撞检测
    /// 检测一个包围盒与多个包围盒的碰撞
    /// 使用向量化加速
    /// </summary>
    public static bool[] BoundingBoxIntersectsBatch(BoundingBox3D target, BoundingBox3D[] boxes)
    {
        if (boxes == null || boxes.Length == 0)
            return Array.Empty<bool>();

        bool[] results = new bool[boxes.Length];

        for (int i = 0; i < boxes.Length; i++)
        {
            results[i] = BoundingBoxIntersects(target, boxes[i]);
        }

        return results;
    }

    /// <summary>
    /// 计算两个运动物体的最近会遇时间 (TCPA - Time to Closest Point of Approach)
    /// 使用向量运算优化
    /// </summary>
    /// <param name="pos1">物体1位置</param>
    /// <param name="vel1">物体1速度</param>
    /// <param name="pos2">物体2位置</param>
    /// <param name="vel2">物体2速度</param>
    /// <returns>最近会遇时间 (秒)，负数表示已经过了最近点</returns>
    public static double CalculateTCPA(
        Position3D pos1, Velocity3D vel1,
        Position3D pos2, Velocity3D vel2)
    {
        double relEast = vel2.East - vel1.East;
        double relNorth = vel2.North - vel1.North;
        double relUp = vel2.Up - vel1.Up;

        double relSpeedSquared = relEast * relEast + relNorth * relNorth + relUp * relUp;

        if (relSpeedSquared < 1e-6)
            return double.PositiveInfinity;

        double midLat = (pos1.Latitude + pos2.Latitude) / 2.0 * DegreesToRadians;
        double cosLat = Math.Cos(midLat);

        double dxMeters = (pos2.Longitude - pos1.Longitude) * DegreesToRadians * EarthRadiusMeters * cosLat;
        double dyMeters = (pos2.Latitude - pos1.Latitude) * DegreesToRadians * EarthRadiusMeters;
        double dzMeters = pos2.Altitude - pos1.Altitude;

        double relativeDistanceDot = dxMeters * relEast + dyMeters * relNorth + dzMeters * relUp;

        return -relativeDistanceDot / relSpeedSquared;
    }

    /// <summary>
    /// 计算最近会遇距离 (DCPA - Distance at Closest Point of Approach)
    /// </summary>
    /// <param name="pos1">物体1位置</param>
    /// <param name="vel1">物体1速度</param>
    /// <param name="pos2">物体2位置</param>
    /// <param name="vel2">物体2速度</param>
    /// <param name="tcpa">最近会遇时间</param>
    /// <returns>最近会遇距离 (米)</returns>
    public static double CalculateDCPA(
        Position3D pos1, Velocity3D vel1,
        Position3D pos2, Velocity3D vel2,
        double tcpa)
    {
        double latRad1 = pos1.Latitude * DegreesToRadians;
        double cosLat1 = Math.Cos(latRad1);
        double futureLon1 = pos1.Longitude + vel1.East * tcpa / (EarthRadiusMeters * cosLat1) * RadiansToDegrees;
        double futureLat1 = pos1.Latitude + vel1.North * tcpa / EarthRadiusMeters * RadiansToDegrees;
        double futureAlt1 = pos1.Altitude + vel1.Up * tcpa;

        double latRad2 = pos2.Latitude * DegreesToRadians;
        double cosLat2 = Math.Cos(latRad2);
        double futureLon2 = pos2.Longitude + vel2.East * tcpa / (EarthRadiusMeters * cosLat2) * RadiansToDegrees;
        double futureLat2 = pos2.Latitude + vel2.North * tcpa / EarthRadiusMeters * RadiansToDegrees;
        double futureAlt2 = pos2.Altitude + vel2.Up * tcpa;

        return Distance(
            new Position3D(futureLon1, futureLat1, futureAlt1),
            new Position3D(futureLon2, futureLat2, futureAlt2));
    }

    /// <summary>
    /// 预测未来某时刻的位置
    /// </summary>
    /// <param name="currentPosition">当前位置</param>
    /// <param name="velocity">当前速度</param>
    /// <param name="timeSeconds">未来时间 (秒)</param>
    /// <returns>预测位置</returns>
    public static Position3D PredictPosition(Position3D currentPosition, Velocity3D velocity, double timeSeconds)
    {
        double latRad = currentPosition.Latitude * DegreesToRadians;
        double cosLat = Math.Cos(latRad);

        double deltaLon = velocity.East * timeSeconds / (EarthRadiusMeters * cosLat) * RadiansToDegrees;
        double deltaLat = velocity.North * timeSeconds / EarthRadiusMeters * RadiansToDegrees;
        double deltaAlt = velocity.Up * timeSeconds;

        return new Position3D(
            currentPosition.Longitude + deltaLon,
            currentPosition.Latitude + deltaLat,
            currentPosition.Altitude + deltaAlt
        );
    }

    /// <summary>
    /// 生成预测轨迹
    /// </summary>
    /// <param name="currentPosition">当前位置</param>
    /// <param name="velocity">当前速度</param>
    /// <param name="predictionHorizon">预测时间范围 (秒)</param>
    /// <param name="timeStep">时间步长 (秒)</param>
    /// <returns>轨迹点列表</returns>
    public static List<TrajectoryPoint> GeneratePredictedTrajectory(
        Position3D currentPosition,
        Velocity3D velocity,
        double predictionHorizon,
        double timeStep = 1.0)
    {
        var trajectory = new List<TrajectoryPoint>();
        int steps = (int)(predictionHorizon / timeStep) + 1;

        for (int i = 0; i < steps; i++)
        {
            double t = i * timeStep;
            var predictedPos = PredictPosition(currentPosition, velocity, t);

            trajectory.Add(new TrajectoryPoint(t, predictedPos, velocity));
        }

        return trajectory;
    }

    /// <summary>
    /// 计算两个轨迹之间的最小距离
    /// 使用双指针法优化计算
    /// </summary>
    public static double MinimumDistanceBetweenTrajectories(
        List<TrajectoryPoint> traj1,
        List<TrajectoryPoint> traj2)
    {
        if (traj1.Count == 0 || traj2.Count == 0)
            return double.MaxValue;

        double minDistance = double.MaxValue;

        int i = 0, j = 0;
        while (i < traj1.Count && j < traj2.Count)
        {
            double dist = Distance(traj1[i].Position, traj2[j].Position);
            if (dist < minDistance)
                minDistance = dist;

            if (i + 1 >= traj1.Count) { j++; continue; }
            if (j + 1 >= traj2.Count) { i++; continue; }

            double distNext1 = Distance(traj1[i + 1].Position, traj2[j].Position);
            double distNext2 = Distance(traj1[i].Position, traj2[j + 1].Position);

            if (distNext1 < distNext2)
                i++;
            else
                j++;
        }

        return minDistance;
    }

    /// <summary>
    /// 批量计算多架无人机之间的最小距离矩阵
    /// </summary>
    /// <param name="positions">无人机位置数组</param>
    /// <returns>距离矩阵 (上三角)</returns>
    public static double[,] CalculateDistanceMatrix(Position3D[] positions)
    {
        int n = positions.Length;
        var matrix = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double dist = Distance(positions[i], positions[j]);
                matrix[i, j] = dist;
                matrix[j, i] = dist;
            }
        }

        return matrix;
    }

    /// <summary>
    /// 计算无人机的安全包围盒
    /// </summary>
    /// <param name="drone">无人机</param>
    /// <param name="safetyMargin">安全裕度 (m)</param>
    /// <returns>包围盒</returns>
    public static BoundingBox3D GetDroneBoundingBox(Drone drone, double safetyMargin = 0)
    {
        double margin = drone.SafetyRadius + safetyMargin;
        double latRad = drone.CurrentPosition.Latitude * DegreesToRadians;
        double cosLat = Math.Cos(latRad);
        double marginLonDeg = margin / (EarthRadiusMeters * cosLat) * RadiansToDegrees;
        double marginLatDeg = margin / EarthRadiusMeters * RadiansToDegrees;

        return new BoundingBox3D(
            new Position3D(
                drone.CurrentPosition.Longitude - marginLonDeg,
                drone.CurrentPosition.Latitude - marginLatDeg,
                drone.CurrentPosition.Altitude - margin
            ),
            new Position3D(
                drone.CurrentPosition.Longitude + marginLonDeg,
                drone.CurrentPosition.Latitude + marginLatDeg,
                drone.CurrentPosition.Altitude + margin
            )
        );
    }

    /// <summary>
    /// 计算点到线段的最短距离
    /// </summary>
    public static double DistancePointToSegment(Position3D point, Position3D segStart, Position3D segEnd)
    {
        double dx = segEnd.Longitude - segStart.Longitude;
        double dy = segEnd.Latitude - segStart.Latitude;
        double dz = segEnd.Altitude - segStart.Altitude;

        double lenSq = dx * dx + dy * dy + dz * dz;
        if (lenSq < 1e-10)
            return Distance(point, segStart);

        double t = ((point.Longitude - segStart.Longitude) * dx +
                    (point.Latitude - segStart.Latitude) * dy +
                    (point.Altitude - segStart.Altitude) * dz) / lenSq;

        t = Math.Max(0, Math.Min(1, t));

        var proj = new Position3D(
            segStart.Longitude + t * dx,
            segStart.Latitude + t * dy,
            segStart.Altitude + t * dz
        );

        return Distance(point, proj);
    }
}
