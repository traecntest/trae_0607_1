using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using UTM.SharedKernel.Models;

namespace UTM.TelemetryService.Gateway;

/// <summary>
/// 遥测数据接入网关
/// 使用 System.IO.Pipelines 构建高吞吐量的数据接入层
/// 支持从网络流中高效读取和解析遥测数据
/// </summary>
public class TelemetryGateway
{
    private readonly ITelemetryDataSink _sink;
    private readonly JsonSerializerOptions _jsonOptions;

    public TelemetryGateway(ITelemetryDataSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultBufferSize = 1024
        };
    }

    /// <summary>
    /// 从流中读取并解析遥测数据
    /// 使用 PipeReader 进行高效的流式读取
    /// </summary>
    /// <param name="stream">输入流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理的数据条数</returns>
    public async Task<int> ProcessStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(
            bufferSize: 4096,
            minimumReadSize: 256,
            leaveOpen: true));

        int totalProcessed = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    if (line.Length > 0)
                    {
                        try
                        {
                            var telemetry = ParseTelemetryData(line);
                            if (telemetry != null)
                            {
                                await _sink.WriteAsync(telemetry, cancellationToken);
                                totalProcessed++;
                            }
                        }
                        catch (JsonException)
                        {
                        }
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }

        return totalProcessed;
    }

    /// <summary>
    /// 尝试从缓冲区读取一行数据
    /// </summary>
    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        SequencePosition? position = buffer.PositionOf((byte)'\n');

        if (position == null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));

        long len = line.Length;
        if (len > 0 && len <= line.First.Span.Length && line.First.Span[(int)(len - 1)] == (byte)'\r')
        {
            line = line.Slice(0, len - 1);
        }

        return true;
    }

    /// <summary>
    /// 解析遥测数据
    /// 使用 ReadOnlySpan 提高解析性能
    /// </summary>
    private TelemetryData? ParseTelemetryData(ReadOnlySequence<byte> line)
    {
        if (line.Length == 0)
            return null;

        byte[]? rented = null;
        Span<byte> span = line.Length <= 256
            ? stackalloc byte[256]
            : (rented = ArrayPool<byte>.Shared.Rent((int)line.Length));

        try
        {
            line.CopyTo(span);
            var jsonSpan = span.Slice(0, (int)line.Length);

            return JsonSerializer.Deserialize<TelemetryData>(jsonSpan, _jsonOptions);
        }
        finally
        {
            if (rented != null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// 批量写入遥测数据（二进制格式，更高性能）
    /// </summary>
    public async Task<int> ProcessBinaryBatchAsync(Stream stream, int count, CancellationToken cancellationToken = default)
    {
        var reader = PipeReader.Create(stream);
        int processed = 0;

        try
        {
            while (processed < count && !cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (buffer.Length >= 128 && processed < count)
                {
                    var telemetry = ParseBinaryTelemetry(buffer.Slice(0, 128));
                    if (telemetry != null)
                    {
                        await _sink.WriteAsync(telemetry, cancellationToken);
                        processed++;
                    }
                    buffer = buffer.Slice(128);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }

        return processed;
    }

    private static TelemetryData? ParseBinaryTelemetry(ReadOnlySequence<byte> data)
    {
        if (data.Length < 128)
            return null;

        var span = data.FirstSpan;
        if (span.Length < 128)
        {
            byte[] temp = new byte[128];
            data.CopyTo(temp);
            span = temp;
        }

        return new TelemetryData
        {
            DroneId = Encoding.UTF8.GetString(span.Slice(0, 32)).TrimEnd('\0'),
            SequenceNumber = BitConverter.ToInt64(span.Slice(32, 8)),
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(span.Slice(40, 8))),
            Position = new Position3D(
                BitConverter.ToDouble(span.Slice(48, 8)),
                BitConverter.ToDouble(span.Slice(56, 8)),
                BitConverter.ToDouble(span.Slice(64, 8))
            ),
            Velocity = new Velocity3D(
                BitConverter.ToDouble(span.Slice(72, 8)),
                BitConverter.ToDouble(span.Slice(80, 8)),
                BitConverter.ToDouble(span.Slice(88, 8))
            ),
            BatteryLevel = BitConverter.ToDouble(span.Slice(96, 8)),
            Status = (DroneStatus)BitConverter.ToInt32(span.Slice(104, 4))
        };
    }
}

/// <summary>
/// 遥测数据接收器接口
/// </summary>
public interface ITelemetryDataSink
{
    ValueTask WriteAsync(TelemetryData data, CancellationToken cancellationToken = default);
}
