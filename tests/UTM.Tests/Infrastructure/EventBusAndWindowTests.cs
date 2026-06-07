using FluentAssertions;
using UTM.SharedKernel.Events;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;

namespace UTM.Tests.Infrastructure;

/// <summary>
/// 内存事件总线单元测试
/// </summary>
public class InMemoryEventBusTests
{
    private readonly IEventBus _eventBus;

    public InMemoryEventBusTests()
    {
        _eventBus = new InMemoryEventBus();
    }

    /// <summary>
    /// 测试发布和订阅
    /// </summary>
    [Fact]
    public async Task PublishAsync_SubscribedHandler_ReceivesEvent()
    {
        var receivedEvent = false;
        TelemetryReceivedEvent? received = null;

        using var subscription = _eventBus.Subscribe<TelemetryReceivedEvent>(evt =>
        {
            receivedEvent = true;
            received = evt;
            return Task.CompletedTask;
        });

        var testEvent = new TelemetryReceivedEvent
        {
            Telemetry = new TelemetryData { DroneId = "DRONE-001" }
        };

        await _eventBus.PublishAsync(testEvent);

        receivedEvent.Should().BeTrue();
        received.Should().NotBeNull();
        received?.Telemetry.DroneId.Should().Be("DRONE-001");
    }

    /// <summary>
    /// 测试取消订阅后不再接收事件
    /// </summary>
    [Fact]
    public async Task PublishAsync_UnsubscribedHandler_DoesNotReceiveEvent()
    {
        var receivedCount = 0;

        var subscription = _eventBus.Subscribe<TelemetryReceivedEvent>(_ =>
        {
            receivedCount++;
            return Task.CompletedTask;
        });

        await _eventBus.PublishAsync(new TelemetryReceivedEvent());
        receivedCount.Should().Be(1);

        subscription.Dispose();

        await _eventBus.PublishAsync(new TelemetryReceivedEvent());
        receivedCount.Should().Be(1);
    }

    /// <summary>
    /// 测试多个订阅者
    /// </summary>
    [Fact]
    public async Task PublishAsync_MultipleSubscribers_AllReceiveEvent()
    {
        int count1 = 0, count2 = 0;

        using var sub1 = _eventBus.Subscribe<TelemetryReceivedEvent>(_ =>
        {
            count1++;
            return Task.CompletedTask;
        });

        using var sub2 = _eventBus.Subscribe<TelemetryReceivedEvent>(_ =>
        {
            count2++;
            return Task.CompletedTask;
        });

        await _eventBus.PublishAsync(new TelemetryReceivedEvent());

        count1.Should().Be(1);
        count2.Should().Be(1);
    }

    /// <summary>
    /// 测试不同事件类型的订阅互相独立
    /// </summary>
    [Fact]
    public async Task PublishAsync_DifferentEventType_DoesNotTriggerHandler()
    {
        var telemetryReceived = false;
        var conflictReceived = false;

        using var sub1 = _eventBus.Subscribe<TelemetryReceivedEvent>(_ =>
        {
            telemetryReceived = true;
            return Task.CompletedTask;
        });

        using var sub2 = _eventBus.Subscribe<ConflictDetectedEvent>(_ =>
        {
            conflictReceived = true;
            return Task.CompletedTask;
        });

        await _eventBus.PublishAsync(new TelemetryReceivedEvent());

        telemetryReceived.Should().BeTrue();
        conflictReceived.Should().BeFalse();
    }
}

/// <summary>
/// 滑动时间窗单元测试
/// </summary>
public class SlidingTimeWindowTests
{
    private static TestItem CreateItem(DateTimeOffset time, string id = "test")
    {
        return new TestItem { Id = id, Timestamp = time };
    }

    private class TestItem
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }

    /// <summary>
    /// 测试添加项目
    /// </summary>
    [Fact]
    public void Add_NewItem_IncreasesCount()
    {
        var window = new SlidingTimeWindow<TestItem>(
            TimeSpan.FromMinutes(5),
            item => item.Timestamp);

        window.Add(CreateItem(DateTimeOffset.UtcNow));

        window.Count.Should().Be(1);
    }

    /// <summary>
    /// 测试过期项目自动移除
    /// </summary>
    [Fact]
    public void Count_OldItems_AreRemoved()
    {
        var window = new SlidingTimeWindow<TestItem>(
            TimeSpan.FromSeconds(1),
            item => item.Timestamp);

        window.Add(CreateItem(DateTimeOffset.UtcNow - TimeSpan.FromSeconds(2)));

        window.Count.Should().Be(0);
    }

    /// <summary>
    /// 测试时间范围内的项目保留
    /// </summary>
    [Fact]
    public void Count_RecentItems_AreKept()
    {
        var window = new SlidingTimeWindow<TestItem>(
            TimeSpan.FromSeconds(5),
            item => item.Timestamp);

        window.Add(CreateItem(DateTimeOffset.UtcNow - TimeSpan.FromSeconds(2)));
        window.Add(CreateItem(DateTimeOffset.UtcNow - TimeSpan.FromSeconds(1)));
        window.Add(CreateItem(DateTimeOffset.UtcNow));

        window.Count.Should().Be(3);
    }

    /// <summary>
    /// 测试获取指定时间范围的项目
    /// </summary>
    [Fact]
    public void GetItemsInRange_ValidRange_ReturnsCorrectItems()
    {
        var window = new SlidingTimeWindow<TestItem>(
            TimeSpan.FromMinutes(10),
            item => item.Timestamp);

        var now = DateTimeOffset.UtcNow;
        window.Add(CreateItem(now - TimeSpan.FromMinutes(5), "a"));
        window.Add(CreateItem(now - TimeSpan.FromMinutes(3), "b"));
        window.Add(CreateItem(now - TimeSpan.FromMinutes(1), "c"));

        var items = window.GetItemsInRange(
            now - TimeSpan.FromMinutes(4),
            now - TimeSpan.FromMinutes(2));

        items.Should().ContainSingle();
        items[0].Id.Should().Be("b");
    }

    /// <summary>
    /// 测试清空窗口
    /// </summary>
    [Fact]
    public void Clear_AllItems_RemovesEverything()
    {
        var window = new SlidingTimeWindow<TestItem>(
            TimeSpan.FromMinutes(5),
            item => item.Timestamp);

        for (int i = 0; i < 10; i++)
            window.Add(CreateItem(DateTimeOffset.UtcNow));

        window.Count.Should().Be(10);

        window.Clear();

        window.Count.Should().Be(0);
    }

    /// <summary>
    /// 测试批量添加
    /// </summary>
    [Fact]
    public void AddRange_MultipleItems_AddsAll()
    {
        var window = new SlidingTimeWindow<TestItem>(
            TimeSpan.FromMinutes(5),
            item => item.Timestamp);

        var items = new[]
        {
            CreateItem(DateTimeOffset.UtcNow, "1"),
            CreateItem(DateTimeOffset.UtcNow, "2"),
            CreateItem(DateTimeOffset.UtcNow, "3")
        };

        window.AddRange(items);

        window.Count.Should().Be(3);
    }
}
