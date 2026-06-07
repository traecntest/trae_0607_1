using UTM.SharedKernel.Models;

namespace UTM.TelemetryService.Simulation;

/// <summary>
/// 无人机遥测数据模拟器
/// 生成逼真的模拟遥测数据，用于测试和演示
/// </summary>
public class DroneSimulator
{
    private readonly object _lock = new();
    private readonly Dictionary<string, SimulatedDrone> _drones = new();
    private readonly Random _random = new(42);

    /// <summary>
    /// 模拟的无人机数量
    /// </summary>
    public int DroneCount => _drones.Count;

    /// <summary>
    /// 添加一架模拟无人机
    /// </summary>
    public void AddDrone(string droneId, Position3D startPosition, DroneType type = DroneType.Multirotor)
    {
        lock (_lock)
        {
            var drone = new SimulatedDrone
            {
                DroneId = droneId,
                Position = startPosition,
                Velocity = new Velocity3D(0, 0, 0),
                Type = type,
                Status = DroneStatus.Idle,
                BatteryLevel = 100.0,
                SequenceNumber = 0
            };

            _drones[droneId] = drone;
        }
    }

    /// <summary>
    /// 批量添加模拟无人机
    /// </summary>
    public void AddDrones(int count, double centerLon, double centerLat, double baseAltitude = 100.0)
    {
        for (int i = 0; i < count; i++)
        {
            string droneId = $"DRONE-{i + 1:D4}";
            double angle = (double)i / count * Math.PI * 2;
            double radius = 500 + _random.NextDouble() * 500;

            double lon = centerLon + Math.Cos(angle) * radius / 111000.0;
            double lat = centerLat + Math.Sin(angle) * radius / 111000.0;
            double alt = baseAltitude + _random.NextDouble() * 50;

            AddDrone(droneId, new Position3D(lon, lat, alt));

            var drone = _drones[droneId];
            drone.Status = DroneStatus.Flying;
            drone.Velocity = new Velocity3D(
                east: (_random.NextDouble() - 0.5) * 20,
                north: (_random.NextDouble() - 0.5) * 20,
                up: (_random.NextDouble() - 0.5) * 2
            );
        }
    }

    /// <summary>
    /// 推进模拟时间，生成新的遥测数据
    /// </summary>
    /// <param name="deltaTime">时间增量 (秒)</param>
    /// <returns>更新后的遥测数据列表</returns>
    public List<TelemetryData> Tick(double deltaTime)
    {
        var telemetryList = new List<TelemetryData>();

        lock (_lock)
        {
            foreach (var drone in _drones.Values.ToList())
            {
                UpdateDrone(drone, deltaTime);
                telemetryList.Add(CreateTelemetry(drone));
            }
        }

        return telemetryList;
    }

    /// <summary>
    /// 获取单架无人机的下一条遥测数据
    /// </summary>
    public TelemetryData? GetNextTelemetry(string droneId, double deltaTime)
    {
        lock (_lock)
        {
            if (!_drones.TryGetValue(droneId, out var drone))
                return null;

            UpdateDrone(drone, deltaTime);
            return CreateTelemetry(drone);
        }
    }

    private void UpdateDrone(SimulatedDrone drone, double deltaTime)
    {
        const double EarthRadius = 6371000.0;
        const double DegToRad = Math.PI / 180.0;
        const double RadToDeg = 180.0 / Math.PI;

        if (drone.Status == DroneStatus.Flying || drone.Status == DroneStatus.Hovering)
        {
            double latRad = drone.Position.Latitude * DegToRad;

            double deltaLon = drone.Velocity.East * deltaTime / (EarthRadius * Math.Cos(latRad)) * RadToDeg;
            double deltaLat = drone.Velocity.North * deltaTime / EarthRadius * RadToDeg;
            double deltaAlt = drone.Velocity.Up * deltaTime;

            drone.Position.Longitude += deltaLon;
            drone.Position.Latitude += deltaLat;
            drone.Position.Altitude += deltaAlt;

            if (drone.Position.Altitude < 0)
            {
                drone.Position.Altitude = 0;
                if (drone.Velocity.Up < 0) drone.Velocity.Up = 0;
            }

            drone.Velocity.East += (_random.NextDouble() - 0.5) * 0.5;
            drone.Velocity.North += (_random.NextDouble() - 0.5) * 0.5;
            drone.Velocity.Up += (_random.NextDouble() - 0.5) * 0.2;

            double speed = drone.Velocity.Speed;
            if (speed > 25.0)
            {
                double scale = 25.0 / speed;
                drone.Velocity.East *= scale;
                drone.Velocity.North *= scale;
                drone.Velocity.Up *= scale;
            }

            drone.BatteryLevel -= deltaTime * 0.01;
            if (drone.BatteryLevel < 0) drone.BatteryLevel = 0;

            drone.SequenceNumber++;
        }
    }

    private static TelemetryData CreateTelemetry(SimulatedDrone drone)
    {
        return new TelemetryData
        {
            DroneId = drone.DroneId,
            SequenceNumber = drone.SequenceNumber,
            Timestamp = DateTimeOffset.UtcNow,
            Position = new Position3D(
                drone.Position.Longitude,
                drone.Position.Latitude,
                drone.Position.Altitude
            ),
            Velocity = new Velocity3D(
                drone.Velocity.East,
                drone.Velocity.North,
                drone.Velocity.Up
            ),
            Acceleration = 0.5,
            Heading = drone.Velocity.Heading,
            Pitch = 5.0,
            Roll = 3.0,
            BatteryLevel = drone.BatteryLevel,
            BatteryVoltage = 11.1 + drone.BatteryLevel / 100.0 * 1.5,
            Status = drone.Status,
            GpsSatellites = 12,
            GpsAccuracy = 1.5,
            SignalStrength = 85.0 + new Random().NextDouble() * 15,
            FlightMode = "AUTO"
        };
    }

    /// <summary>
    /// 获取所有无人机状态
    /// </summary>
    public List<Drone> GetAllDrones()
    {
        lock (_lock)
        {
            return _drones.Values.Select(d => new Drone
            {
                Id = d.DroneId,
                Name = d.DroneId,
                Type = d.Type,
                MaxSpeed = 25.0,
                MaxAltitude = 500.0,
                SafetyRadius = 5.0,
                Status = d.Status,
                CurrentPosition = new Position3D(
                    d.Position.Longitude,
                    d.Position.Latitude,
                    d.Position.Altitude
                ),
                Velocity = new Velocity3D(
                    d.Velocity.East,
                    d.Velocity.North,
                    d.Velocity.Up
                ),
                BatteryLevel = d.BatteryLevel,
                LastUpdateTime = DateTimeOffset.UtcNow
            }).ToList();
        }
    }

    /// <summary>
    /// 设置无人机速度
    /// </summary>
    public void SetVelocity(string droneId, Velocity3D velocity)
    {
        lock (_lock)
        {
            if (_drones.TryGetValue(droneId, out var drone))
            {
                drone.Velocity = velocity;
            }
        }
    }

    /// <summary>
    /// 设置无人机状态
    /// </summary>
    public void SetDroneStatus(string droneId, DroneStatus status)
    {
        lock (_lock)
        {
            if (_drones.TryGetValue(droneId, out var drone))
            {
                drone.Status = status;
            }
        }
    }

    /// <summary>
    /// 清除所有模拟无人机
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            _drones.Clear();
        }
    }

    /// <summary>
    /// 获取所有无人机ID
    /// </summary>
    public IEnumerable<string> GetAllDroneIds()
    {
        lock (_lock)
        {
            return _drones.Keys.ToList();
        }
    }

    private class SimulatedDrone
    {
        public string DroneId = string.Empty;
        public Position3D Position;
        public Velocity3D Velocity;
        public DroneType Type;
        public DroneStatus Status;
        public double BatteryLevel;
        public long SequenceNumber;
    }
}
