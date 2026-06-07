using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using UTM.SharedKernel.Geometry;
using UTM.SharedKernel.Infrastructure;
using UTM.SharedKernel.Models;
using UTM.TelemetryService.Pipeline;

namespace UTM.Tests.Performance;

/// <summary>
/// 几何计算性能基准测试
/// </summary>
public class GeometryBenchmarks
{
    private Position3D[] _positions = null!;
    private Position3D _origin;

    [Params(100, 500, 1000)]
    public int PointCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _origin = new Position3D(116.3972, 39.9075, 100.0);
        _positions = new Position3D[PointCount];

        for (int i = 0; i < PointCount; i++)
        {
            double angle = (double)i / PointCount * Math.PI * 2;
            double radius = 100 + random.NextDouble() * 400;

            _positions[i] = new Position3D(
                116.3972 + Math.Cos(angle) * radius / 111000.0,
                39.9075 + Math.Sin(angle) * radius / 111000.0,
                100.0 + random.NextDouble() * 50
            );
        }
    }

    [Benchmark]
    public double Distance_Single()
    {
        return GeometryCalculator.Distance(_origin, _positions[0]);
    }

    [Benchmark]
    public double[] DistanceBatch_AllPoints()
    {
        var originVec = GeometryCalculator.ToLocalMeters(_origin, _origin);
        var pointVecs = _positions
            .Select(p => GeometryCalculator.ToLocalMeters(p, _origin))
            .ToArray();

        return GeometryCalculator.DistanceBatch(originVec, pointVecs);
    }

    [Benchmark]
    public double[,] CalculateDistanceMatrix()
    {
        return GeometryCalculator.CalculateDistanceMatrix(_positions);
    }

    [Benchmark]
    public bool BoundingBoxIntersects()
    {
        var box1 = new BoundingBox3D(
            new Position3D(0, 0, 0),
            new Position3D(10, 10, 10));
        var box2 = new BoundingBox3D(
            new Position3D(5, 5, 5),
            new Position3D(15, 15, 15));

        return GeometryCalculator.BoundingBoxIntersects(box1, box2);
    }

    [Benchmark]
    public double CalculateTCPA()
    {
        var pos1 = new Position3D(0, 0, 100);
        var vel1 = new Velocity3D(10, 0, 0);
        var pos2 = new Position3D(0.001, 0, 100);
        var vel2 = new Velocity3D(-10, 0, 0);

        return GeometryCalculator.CalculateTCPA(pos1, vel1, pos2, vel2);
    }

    [Benchmark]
    public List<TrajectoryPoint> GeneratePredictedTrajectory()
    {
        var pos = new Position3D(116.3972, 39.9075, 100.0);
        var vel = new Velocity3D(10.0, 5.0, 1.0);

        return GeometryCalculator.GeneratePredictedTrajectory(pos, vel, 60.0, 1.0);
    }
}

/// <summary>
/// 遥测管道性能基准测试
/// </summary>
public class TelemetryPipelineBenchmarks
{
    private IEventBus _eventBus = null!;
    private ChannelTelemetryPipeline _pipeline = null!;
    private TelemetryData[] _testData = null!;

    [Params(100, 1000, 10000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new InMemoryEventBus();
        _pipeline = new ChannelTelemetryPipeline(_eventBus, 100000);

        _testData = new TelemetryData[MessageCount];
        var random = new Random(42);

        for (int i = 0; i < MessageCount; i++)
        {
            _testData[i] = new TelemetryData
            {
                DroneId = $"DRONE-{i % 100:D4}",
                SequenceNumber = i,
                Timestamp = DateTimeOffset.UtcNow,
                Position = new Position3D(
                    116.3972 + random.NextDouble() * 0.01,
                    39.9075 + random.NextDouble() * 0.01,
                    100.0 + random.NextDouble() * 50
                ),
                Velocity = new Velocity3D(
                    random.NextDouble() * 20 - 10,
                    random.NextDouble() * 20 - 10,
                    random.NextDouble() * 4 - 2
                )
            };
        }
    }

    [Benchmark]
    public async Task WriteSingle()
    {
        await _pipeline.WriteAsync(_testData[0]);
    }

    [Benchmark]
    public async Task WriteBatch()
    {
        await _pipeline.WriteBatchAsync(_testData);
    }

    [Benchmark]
    public async Task WriteAndReadBatch()
    {
        await _pipeline.WriteBatchAsync(_testData);

        int readCount = 0;
        await foreach (var _ in _pipeline.ReadAllAsync())
        {
            readCount++;
            if (readCount >= MessageCount) break;
        }
    }
}

/// <summary>
/// 性能测试运行器
/// </summary>
public class PerformanceBenchmarkRunner
{
    public static void Run(string[] args)
    {
        var summary = BenchmarkRunner.Run<GeometryBenchmarks>();
        Console.WriteLine(summary);
    }
}
