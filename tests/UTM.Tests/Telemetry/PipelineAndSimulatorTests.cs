using System.Threading.Channels;
using FluentAssertions;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;
using UTM.TelemetryService.Pipeline;
using UTM.TelemetryService.Simulation;

namespace UTM.Tests.Telemetry;

/// <summary>
/// 遥测管道单元测试
/// </summary>
public class ChannelTelemetryPipelineTests
{
    private readonly IEventBus _eventBus;
    private readonly ChannelTelemetryPipeline _pipeline;

    public ChannelTelemetryPipelineTests()
    {
        _eventBus = new InMemoryEventBus();
        _pipeline = new ChannelTelemetryPipeline(_eventBus, 100);
    }

    /// <summary>
    /// 测试写入单条遥测数据
    /// </summary>
    [Fact]
    public async Task WriteAsync_SingleItem_Succeeds()
    {
        var telemetry = new TelemetryData { DroneId = "DRONE-001", SequenceNumber = 1 };

        await _pipeline.WriteAsync(telemetry);

        _pipeline.QueueLength.Should().Be(1);
    }

    /// <summary>
    /// 测试批量写入遥测数据
    /// </summary>
    [Fact]
    public async Task WriteBatchAsync_MultipleItems_Succeeds()
    {
        var telemetryList = Enumerable.Range(1, 10)
            .Select(i => new TelemetryData
            {
                DroneId = $"DRONE-{i:D3}",
                SequenceNumber = i
            })
            .ToList();

        await _pipeline.WriteBatchAsync(telemetryList);

        _pipeline.QueueLength.Should().Be(10);
    }

    /// <summary>
    /// 测试读取遥测数据
    /// </summary>
    [Fact]
    public async Task ReadAllAsync_WrittenItems_CanBeRead()
    {
        var telemetry = new TelemetryData { DroneId = "DRONE-001", SequenceNumber = 42 };

        await _pipeline.WriteAsync(telemetry);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        TelemetryData? received = null;
        await foreach (var item in _pipeline.ReadAllAsync(cts.Token))
        {
            received = item;
            break;
        }

        received.Should().NotBeNull();
        received?.DroneId.Should().Be("DRONE-001");
        received?.SequenceNumber.Should().Be(42);
    }

    /// <summary>
    /// 测试总处理数统计
    /// </summary>
    [Fact]
    public async Task TotalProcessed_AfterReading_Increments()
    {
        var initialCount = _pipeline.TotalProcessed;

        for (int i = 0; i < 5; i++)
        {
            await _pipeline.WriteAsync(new TelemetryData { DroneId = $"DRONE-{i}" });
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        int readCount = 0;
        await foreach (var _ in _pipeline.ReadAllAsync(cts.Token))
        {
            readCount++;
            if (readCount >= 5) break;
        }

        _pipeline.TotalProcessed.Should().Be(initialCount + 5);
    }

    /// <summary>
    /// 测试背压控制 - 通道满时会等待
    /// </summary>
    [Fact]
    public async Task WriteAsync_ChannelFull_BlocksUntilSpaceAvailable()
    {
        var smallPipeline = new ChannelTelemetryPipeline(_eventBus, 5);

        for (int i = 0; i < 5; i++)
        {
            await smallPipeline.WriteAsync(new TelemetryData { DroneId = $"DRONE-{i}" });
        }

        smallPipeline.QueueLength.Should().Be(5);

        var writeTask = smallPipeline.WriteAsync(new TelemetryData { DroneId = "DRONE-5" });
        writeTask.IsCompleted.Should().BeFalse();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var _ in smallPipeline.ReadAllAsync(cts.Token))
        {
            break;
        }

        await Task.Delay(50);
        writeTask.IsCompleted.Should().BeTrue();
    }
}

/// <summary>
/// 无人机模拟器单元测试
/// </summary>
public class DroneSimulatorTests
{
    private readonly DroneSimulator _simulator;

    public DroneSimulatorTests()
    {
        _simulator = new DroneSimulator();
    }

    /// <summary>
    /// 测试添加单架无人机
    /// </summary>
    [Fact]
    public void AddDrone_Single_IncreasesCount()
    {
        var position = new Position3D(116.3972, 39.9075, 100.0);

        _simulator.AddDrone("DRONE-001", position);

        _simulator.DroneCount.Should().Be(1);
    }

    /// <summary>
    /// 测试批量添加无人机
    /// </summary>
    [Fact]
    public void AddDrones_Multiple_IncreasesCount()
    {
        _simulator.AddDrones(50, 116.3972, 39.9075, 100.0);

        _simulator.DroneCount.Should().Be(50);
    }

    /// <summary>
    /// 测试模拟器推进后位置会变化
    /// </summary>
    [Fact]
    public void Tick_FlyingDrone_PositionChanges()
    {
        var startPos = new Position3D(116.3972, 39.9075, 100.0);
        _simulator.AddDrone("DRONE-001", startPos);
        _simulator.SetVelocity("DRONE-001", new Velocity3D(10.0, 0.0, 0.0));
        _simulator.SetDroneStatus("DRONE-001", DroneStatus.Flying);

        var initialTelemetry = _simulator.GetNextTelemetry("DRONE-001", 0);
        var telemetryAfter = _simulator.GetNextTelemetry("DRONE-001", 1.0);

        telemetryAfter.Should().NotBeNull();
        telemetryAfter?.Position.Longitude.Should().NotBe(startPos.Longitude);
    }

    /// <summary>
    /// 测试获取所有无人机
    /// </summary>
    [Fact]
    public void GetAllDrones_AfterAdding_ReturnsAll()
    {
        _simulator.AddDrones(10, 116.3972, 39.9075);

        var drones = _simulator.GetAllDrones();

        drones.Should().HaveCount(10);
        drones.All(d => !string.IsNullOrEmpty(d.Id)).Should().BeTrue();
    }

    /// <summary>
    /// 测试Tick返回所有无人机的遥测数据
    /// </summary>
    [Fact]
    public void Tick_MultipleDrones_ReturnsAllTelemetry()
    {
        _simulator.AddDrones(20, 116.3972, 39.9075);

        var telemetryList = _simulator.Tick(0.1);

        telemetryList.Should().HaveCount(20);
        telemetryList.All(t => !string.IsNullOrEmpty(t.DroneId)).Should().BeTrue();
    }

    /// <summary>
    /// 测试序列号码递增
    /// </summary>
    [Fact]
    public void GetNextTelemetry_MultipleCalls_IncrementsSequenceNumber()
    {
        _simulator.AddDrone("DRONE-001", new Position3D(0, 0, 100));
        _simulator.SetDroneStatus("DRONE-001", DroneStatus.Flying);

        var t1 = _simulator.GetNextTelemetry("DRONE-001", 0.1);
        var t2 = _simulator.GetNextTelemetry("DRONE-001", 0.1);
        var t3 = _simulator.GetNextTelemetry("DRONE-001", 0.1);

        t1.Should().NotBeNull();
        t2.Should().NotBeNull();
        t3.Should().NotBeNull();

        t2?.SequenceNumber.Should().BeGreaterThan(t1?.SequenceNumber ?? 0);
        t3?.SequenceNumber.Should().BeGreaterThan(t2?.SequenceNumber ?? 0);
    }
}
