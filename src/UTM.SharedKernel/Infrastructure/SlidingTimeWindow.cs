using System.Collections;

namespace UTM.SharedKernel.Infrastructure;

/// <summary>
/// 时间窗口滑动集合
/// 用于实现基于时间窗的滑动检测算法
/// 自动移除过期的数据项
/// </summary>
/// <typeparam name="T">数据项类型</typeparam>
public class SlidingTimeWindow<T> : IReadOnlyCollection<T>
    where T : class
{
    private readonly LinkedList<(DateTimeOffset Time, T Item)> _items = new();
    private readonly TimeSpan _windowSize;
    private readonly Func<T, DateTimeOffset> _timeSelector;
    private readonly object _lock = new();

    /// <summary>
    /// 窗口大小
    /// </summary>
    public TimeSpan WindowSize => _windowSize;

    /// <summary>
    /// 当前窗口内的项目数量
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                CleanupExpired();
                return _items.Count;
            }
        }
    }

    /// <summary>
    /// 创建一个新的滑动时间窗口
    /// </summary>
    /// <param name="windowSize">窗口大小</param>
    /// <param name="timeSelector">从项目中提取时间的函数</param>
    public SlidingTimeWindow(TimeSpan windowSize, Func<T, DateTimeOffset> timeSelector)
    {
        _windowSize = windowSize;
        _timeSelector = timeSelector ?? throw new ArgumentNullException(nameof(timeSelector));
    }

    /// <summary>
    /// 添加一个项目到窗口
    /// </summary>
    public void Add(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        var time = _timeSelector(item);

        lock (_lock)
        {
            var node = _items.Last;
            while (node != null && node.Value.Time > time)
                node = node.Previous;

            if (node == null)
                _items.AddFirst((time, item));
            else
                _items.AddAfter(node, (time, item));

            CleanupExpired();
        }
    }

    /// <summary>
    /// 添加多个项目
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
            Add(item);
    }

    /// <summary>
    /// 清理过期项目
    /// </summary>
    private void CleanupExpired()
    {
        var cutoff = DateTimeOffset.UtcNow - _windowSize;

        while (_items.Count > 0 && _items.First!.Value.Time < cutoff)
            _items.RemoveFirst();
    }

    /// <summary>
    /// 获取窗口内所有项目
    /// </summary>
    public IReadOnlyList<T> GetItems()
    {
        lock (_lock)
        {
            CleanupExpired();
            return _items.Select(x => x.Item).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 获取指定时间范围内的项目
    /// </summary>
    public IReadOnlyList<T> GetItemsInRange(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        lock (_lock)
        {
            CleanupExpired();
            return _items
                .Where(x => x.Time >= startTime && x.Time <= endTime)
                .Select(x => x.Item)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// 清空窗口
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return GetItems().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 带时间戳的项目包装器
/// </summary>
public interface ITimestamped
{
    DateTimeOffset Timestamp { get; }
}
