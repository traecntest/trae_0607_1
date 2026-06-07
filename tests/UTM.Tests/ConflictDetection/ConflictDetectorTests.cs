using FluentAssertions;
using UTM.ConflictDetectionService.Detection;
using UTM.SharedKernel.Events;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;

namespace UTM.Tests.ConflictDetection;

/// <summary>
/// 冲突检测单元测试
/// </summary>
public class TimeWindowConflictDetectorTests
{
    private readonly IEventBus _eventBus;
    private readonly TimeWindowConflictDetector _detector;

    public TimeWindowConflictDetectorTests()
    {
        _eventBus = new InMemoryEventBus();
        _detector = new TimeWindowConflictDetector(_eventBus)
        {
            SafetyDistance = 10.0,
            PredictionHorizon = 60.0
        };
    }

    private static Drone CreateDrone(string id, double lon, double lat, double alt,
        double eastVel = 0, double northVel = 0, double upVel = 0)
    {
        return new Drone
        {
            Id = id,
            Status = DroneStatus.Flying,
            SafetyRadius = 2.0,
            CurrentPosition = new Position3D(lon, lat, alt),
            Velocity = new Velocity3D(eastVel, northVel, upVel)
        };
    }

    /// <summary>
    /// 测试两架相距很远的无人机没有冲突
    /// </summary>
    [Fact]
    public async Task DetectConflictsAsync_FarAwayDrones_NoConflicts()
    {
        var drone1 = CreateDrone("DRONE-001", 116.0, 39.0, 100.0);
        var drone2 = CreateDrone("DRONE-002", 117.0, 40.0, 100.0);

        var conflicts = await _detector.DetectConflictsAsync(
            new[] { drone1, drone2 });

        conflicts.Should().BeEmpty();
    }

    /// <summary>
    /// 测试两架靠近的无人机会产生冲突
    /// </summary>
    [Fact]
    public async Task DetectConflictsAsync_CloseDrones_DetectsConflict()
    {
        var baseLon = 116.3972;
        var baseLat = 39.9075;

        var drone1 = CreateDrone("DRONE-001", baseLon, baseLat, 100.0, eastVel: 5.0);
        var drone2 = CreateDrone("DRONE-002", baseLon + 100.0 / 111000.0, baseLat, 100.0, eastVel: -5.0);

        var conflicts = await _detector.DetectConflictsAsync(
            new[] { drone1, drone2 });

        conflicts.Should().NotBeEmpty();
        conflicts[0].DroneIds.Should().Contain("DRONE-001").And.Contain("DRONE-002");
    }

    /// <summary>
    /// 测试静止且远离的无人机没有冲突
    /// </summary>
    [Fact]
    public async Task DetectConflictsAsync_StationaryFarDrones_NoConflict()
    {
        var drone1 = CreateDrone("DRONE-001", 116.0, 39.0, 100.0);
        var drone2 = CreateDrone("DRONE-002", 116.1, 39.1, 100.0);

        var conflicts = await _detector.DetectConflictsAsync(
            new[] { drone1, drone2 });

        conflicts.Should().BeEmpty();
    }

    /// <summary>
    /// 测试只有一架无人机时没有冲突
    /// </summary>
    [Fact]
    public async Task DetectConflictsAsync_SingleDrone_NoConflicts()
    {
        var drone = CreateDrone("DRONE-001", 116.0, 39.0, 100.0);

        var conflicts = await _detector.DetectConflictsAsync(
            new[] { drone });

        conflicts.Should().BeEmpty();
    }

    /// <summary>
    /// 测试相向飞行的无人机产生高优先级冲突
    /// </summary>
    [Fact]
    public async Task DetectConflictsAsync_HeadOnDrones_HighSeverityConflict()
    {
        var baseLon = 116.3972;
        var baseLat = 39.9075;

        var drone1 = CreateDrone("DRONE-001", baseLon, baseLat, 100.0, eastVel: 20.0);
        var drone2 = CreateDrone("DRONE-002", baseLon + 200.0 / 111000.0, baseLat, 100.0, eastVel: -20.0);

        var conflicts = await _detector.DetectConflictsAsync(
            new[] { drone1, drone2 });

        conflicts.Should().NotBeEmpty();
        conflicts[0].Severity.Should().BeOneOf(
            ConflictSeverity.Critical,
            ConflictSeverity.High);
        conflicts[0].TimeToCollision.Should().BeGreaterThan(0);
        conflicts[0].TimeToCollision.Should().BeLessThan(10);
    }

    /// <summary>
    /// 测试着陆状态的无人机不参与冲突检测
    /// </summary>
    [Fact]
    public async Task DetectConflictsAsync_LandedDrone_Skipped()
    {
        var drone1 = CreateDrone("DRONE-001", 116.0, 39.0, 100.0);
        drone1.Status = DroneStatus.Landed;

        var drone2 = CreateDrone("DRONE-002", 116.0001, 39.0001, 100.0);

        var conflicts = await _detector.DetectConflictsAsync(
            new[] { drone1, drone2 });

        conflicts.Should().BeEmpty();
    }

    /// <summary>
    /// 测试检测单架无人机与其他无人机的冲突
    /// </summary>
    [Fact]
    public async Task DetectDroneConflictsAsync_ValidInput_ReturnsConflicts()
    {
        var baseLon = 116.3972;
        var baseLat = 39.9075;

        var target = CreateDrone("TARGET", baseLon, baseLat, 100.0, eastVel: 10.0);
        var other1 = CreateDrone("OTHER-1", baseLon + 150.0 / 111000.0, baseLat, 100.0, eastVel: -10.0);
        var other2 = CreateDrone("OTHER-2", baseLon, baseLat + 0.5, 100.0);

        var conflicts = await _detector.DetectDroneConflictsAsync(
            target, new[] { other1, other2 });

        conflicts.Should().NotBeEmpty();
        conflicts.All(c => c.DroneIds.Contains("TARGET")).Should().BeTrue();
    }

    /// <summary>
    /// 测试安全距离配置
    /// </summary>
    [Fact]
    public void SafetyDistance_SetValue_UpdatesCorrectly()
    {
        _detector.SafetyDistance = 50.0;
        _detector.SafetyDistance.Should().Be(50.0);
    }

    /// <summary>
    /// 测试预测时间范围配置
    /// </summary>
    [Fact]
    public void PredictionHorizon_SetValue_UpdatesCorrectly()
    {
        _detector.PredictionHorizon = 120.0;
        _detector.PredictionHorizon.Should().Be(120.0);
    }
}
