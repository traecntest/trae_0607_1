using System.Threading.Channels;
using UTM.SharedKernel.Events;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;
using UTM.TelemetryService.Gateway;

namespace UTM.TelemetryService.Pipeline;

/// <summary>
/// 遥测数据管道
/// 使用Channel实现背压控制的高性能数据管道
/// </summary>
public interface ITelemetryPipeline : ITelemetryDataSink
{
    /// <summary>
    /// 批量写入遥测数据
    /// </summary>
    ValueTask WriteBatchAsync(IEnumerable<TelemetryData> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取异步流读取器
    /// </summary>
    IAsyncEnumerable<TelemetryData> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 当前队列长度
    /// </summary>
    int QueueLength { get; }

    /// <summary>
    /// 已处理的消息总数
    /// </summary>
    long TotalProcessed { get; }
}

/// <summary>
/// 基于Channel的遥测数据管道实现
/// </summary>
public class ChannelTelemetryPipeline : ITelemetryPipeline
{
    private readonly Channel<TelemetryData> _channel;
    private readonly IEventBus _eventBus;
    private long _totalProcessed;

    /// <inheritdoc />
    public int QueueLength => _channel.Reader.Count;

    /// <inheritdoc />
    public long TotalProcessed => Interlocked.Read(ref _totalProcessed);

    public ChannelTelemetryPipeline(IEventBus eventBus, int capacity = 10000)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        };

        _channel = Channel.CreateBounded<TelemetryData>(options);
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(TelemetryData data, CancellationToken cancellationToken = default)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        await _channel.Writer.WriteAsync(data, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask WriteBatchAsync(IEnumerable<TelemetryData> data, CancellationToken cancellationToken = default)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        foreach (var item in data)
        {
            await _channel.Writer.WriteAsync(item, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TelemetryData> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var item))
            {
                Interlocked.Increment(ref _totalProcessed);
                yield return item;
            }
        }
    }

    /// <summary>
    /// 完成写入
    /// </summary>
    public void Complete()
    {
        _channel.Writer.Complete();
    }
}
