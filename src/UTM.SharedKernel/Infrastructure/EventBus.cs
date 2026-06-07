using System.Collections.Concurrent;
using UTM.SharedKernel.Events;

namespace UTM.SharedKernel.Infrastructure;

/// <summary>
/// 内存事件总线
/// 实现发布-订阅模式，用于服务内的事件驱动通信
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;

    /// <summary>
    /// 订阅事件
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler)
        where TEvent : IDomainEvent;
}

/// <summary>
/// 基于内存的事件总线实现
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var eventType = typeof(TEvent);

        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            var handlerList = handlers.ToList();
            return Task.WhenAll(handlerList.Cast<Func<TEvent, Task>>()
                .Select(h => SafeInvokeHandler(h, @event, cancellationToken)));
        }

        return Task.CompletedTask;
    }

    private static async Task SafeInvokeHandler<TEvent>(
        Func<TEvent, Task> handler,
        TEvent @event,
        CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        try
        {
            await handler(@event);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in event handler for {typeof(TEvent).Name}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler)
        where TEvent : IDomainEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);

        lock (_lock)
        {
            var handlers = _handlers.GetOrAdd(eventType, _ => new List<object>());
            handlers.Add(handler);
        }

        return new Subscription<TEvent>(this, handler);
    }

    private void Unsubscribe<TEvent>(Func<TEvent, Task> handler)
        where TEvent : IDomainEvent
    {
        var eventType = typeof(TEvent);

        lock (_lock)
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                    _handlers.TryRemove(eventType, out _);
            }
        }
    }

    private class Subscription<TEvent> : IDisposable
        where TEvent : IDomainEvent
    {
        private readonly InMemoryEventBus _bus;
        private readonly Func<TEvent, Task> _handler;
        private bool _disposed;

        public Subscription(InMemoryEventBus bus, Func<TEvent, Task> handler)
        {
            _bus = bus;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _bus.Unsubscribe(_handler);
        }
    }
}

/// <summary>
/// 事件总线扩展方法
/// </summary>
public static class EventBusExtensions
{
    /// <summary>
    /// 同步发布事件
    /// </summary>
    public static void Publish<TEvent>(this IEventBus bus, TEvent @event)
        where TEvent : IDomainEvent
    {
        bus.PublishAsync(@event).GetAwaiter().GetResult();
    }
}
