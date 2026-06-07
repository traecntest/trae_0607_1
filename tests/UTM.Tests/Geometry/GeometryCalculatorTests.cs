using FluentAssertions;
using UTM.SharedKernel.Geometry;
using UTM.SharedKernel.Models;

namespace UTM.Tests.Geometry;

/// <summary>
/// 几何计算工具单元测试
/// </summary>
public class GeometryCalculatorTests
{
    /// <summary>
    /// 测试两点距离计算
    /// </summary>
    [Fact]
    public void Distance_SamePosition_ReturnsZero()
    {
        var p1 = new Position3D(116.3972, 39.9075, 100.0);
        var p2 = new Position3D(116.3972, 39.9075, 100.0);

        double distance = GeometryCalculator.Distance(p1, p2);

        distance.Should().BeApproximately(0.0, 0.001);
    }

    /// <summary>
    /// 测试水平距离计算（大约1度纬度≈111km）
    /// </summary>
    [Fact]
    public void Distance_OneDegreeLatitude_ReturnsApproximate111km()
    {
        var p1 = new Position3D(0.0, 0.0, 0.0);
        var p2 = new Position3D(0.0, 1.0, 0.0);

        double distance = GeometryCalculator.Distance(p1, p2);

        distance.Should().BeApproximately(111000.0, 2000.0);
    }

    /// <summary>
    /// 测试垂直距离计算
    /// </summary>
    [Fact]
    public void Distance_VerticalDifference_ReturnsAltitudeDifference()
    {
        var p1 = new Position3D(116.3972, 39.9075, 100.0);
        var p2 = new Position3D(116.3972, 39.9075, 200.0);

        double distance = GeometryCalculator.Distance(p1, p2);

        distance.Should().BeApproximately(100.0, 0.5);
    }

    /// <summary>
    /// 测试TCPA计算 - 相向而行的情况
    /// </summary>
    [Fact]
    public void CalculateTCPA_ClosingVehicles_ReturnsPositiveTime()
    {
        var pos1 = new Position3D(0.0, 0.0, 100.0);
        var vel1 = new Velocity3D(10.0, 0.0, 0.0);

        var pos2 = new Position3D(
            2000.0 / 111000.0,
            0.0,
            100.0);
        var vel2 = new Velocity3D(-10.0, 0.0, 0.0);

        double tcpa = GeometryCalculator.CalculateTCPA(pos1, vel1, pos2, vel2);

        tcpa.Should().BeGreaterThan(0);
        tcpa.Should().BeApproximately(100.0, 10.0);
    }

    /// <summary>
    /// 测试TCPA计算 - 相离而行的情况
    /// </summary>
    [Fact]
    public void CalculateTCPA_SeparatingVehicles_ReturnsNegativeTime()
    {
        var pos1 = new Position3D(0.0, 0.0, 100.0);
        var vel1 = new Velocity3D(-10.0, 0.0, 0.0);

        var pos2 = new Position3D(0.001, 0.0, 100.0);
        var vel2 = new Velocity3D(10.0, 0.0, 0.0);

        double tcpa = GeometryCalculator.CalculateTCPA(pos1, vel1, pos2, vel2);

        tcpa.Should().BeLessThan(0);
    }

    /// <summary>
    /// 测试位置预测
    /// </summary>
    [Fact]
    public void PredictPosition_StraightLine_MovesCorrectly()
    {
        var startPos = new Position3D(116.3972, 39.9075, 100.0);
        var velocity = new Velocity3D(0.0, 10.0, 0.0);

        var predicted = GeometryCalculator.PredictPosition(startPos, velocity, 10.0);

        predicted.Altitude.Should().BeApproximately(100.0, 0.1);

        double latDiff = (predicted.Latitude - startPos.Latitude) * 111000.0;
        latDiff.Should().BeApproximately(100.0, 5.0);
    }

    /// <summary>
    /// 测试包围盒相交检测 - 相交的情况
    /// </summary>
    [Fact]
    public void BoundingBoxIntersects_OverlappingBoxes_ReturnsTrue()
    {
        var box1 = new BoundingBox3D(
            new Position3D(0.0, 0.0, 0.0),
            new Position3D(10.0, 10.0, 10.0));

        var box2 = new BoundingBox3D(
            new Position3D(5.0, 5.0, 5.0),
            new Position3D(15.0, 15.0, 15.0));

        bool intersects = GeometryCalculator.BoundingBoxIntersects(box1, box2);

        intersects.Should().BeTrue();
    }

    /// <summary>
    /// 测试包围盒相交检测 - 不相交的情况
    /// </summary>
    [Fact]
    public void BoundingBoxIntersects_SeparateBoxes_ReturnsFalse()
    {
        var box1 = new BoundingBox3D(
            new Position3D(0.0, 0.0, 0.0),
            new Position3D(10.0, 10.0, 10.0));

        var box2 = new BoundingBox3D(
            new Position3D(20.0, 20.0, 20.0),
            new Position3D(30.0, 30.0, 30.0));

        bool intersects = GeometryCalculator.BoundingBoxIntersects(box1, box2);

        intersects.Should().BeFalse();
    }

    /// <summary>
    /// 测试预测轨迹生成
    /// </summary>
    [Fact]
    public void GeneratePredictedTrajectory_ValidInputs_ReturnsCorrectPoints()
    {
        var startPos = new Position3D(116.3972, 39.9075, 100.0);
        var velocity = new Velocity3D(0.0, 10.0, 0.0);

        var trajectory = GeometryCalculator.GeneratePredictedTrajectory(startPos, velocity, 10.0, 2.0);

        trajectory.Should().HaveCount(6);
        trajectory[0].TimeOffset.Should().Be(0);
        trajectory[5].TimeOffset.Should().Be(10.0);
    }

    /// <summary>
    /// 测试批量距离计算
    /// </summary>
    [Fact]
    public void DistanceBatch_MultiplePoints_ReturnsCorrectDistances()
    {
        var origin = new System.Numerics.Vector3(0, 0, 0);
        var points = new[]
        {
            new System.Numerics.Vector3(3, 4, 0),
            new System.Numerics.Vector3(0, 0, 5),
            new System.Numerics.Vector3(1, 1, 1),
            new System.Numerics.Vector3(10, 0, 0)
        };

        var distances = GeometryCalculator.DistanceBatch(origin, points);

        distances.Should().HaveCount(4);
        distances[0].Should().BeApproximately(5.0, 0.001);
        distances[1].Should().BeApproximately(5.0, 0.001);
        distances[3].Should().BeApproximately(10.0, 0.001);
    }
}
