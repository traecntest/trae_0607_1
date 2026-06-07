using Microsoft.AspNetCore.Mvc;
using UTM.ConflictDetectionService.Detection;
using UTM.SharedKernel.Events;
using UTM.SharedKernel.Models;
using UTM.TelemetryService.Services;

namespace UTM.ApiGateway.Controllers;

/// <summary>
/// 冲突检测API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConflictsController : ControllerBase
{
    private readonly IConflictDetector _conflictDetector;
    private readonly IDroneStateStore _droneStateStore;
    private readonly ILogger<ConflictsController> _logger;

    public ConflictsController(
        IConflictDetector conflictDetector,
        IDroneStateStore droneStateStore,
        ILogger<ConflictsController> logger)
    {
        _conflictDetector = conflictDetector;
        _droneStateStore = droneStateStore;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前活跃冲突列表
    /// </summary>
    /// <returns>活跃冲突列表</returns>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<ConflictDetectedEvent>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<ConflictDetectedEvent>> GetActiveConflicts()
    {
        if (_conflictDetector is TimeWindowConflictDetector detector)
        {
            return Ok(detector.GetActiveConflicts());
        }

        return Ok(Array.Empty<ConflictDetectedEvent>());
    }

    /// <summary>
    /// 手动触发冲突检测
    /// </summary>
    /// <returns>检测到的冲突列表</returns>
    [HttpPost("detect")]
    [ProducesResponseType(typeof(IEnumerable<ConflictDetectedEvent>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ConflictDetectedEvent>>> DetectConflicts()
    {
        var drones = _droneStateStore.GetAllDrones().ToList();
        var conflicts = await _conflictDetector.DetectConflictsAsync(drones);
        return Ok(conflicts);
    }

    /// <summary>
    /// 检测指定无人机的冲突
    /// </summary>
    /// <param name="droneId">无人机ID</param>
    /// <returns>相关冲突列表</returns>
    [HttpGet("drone/{droneId}")]
    [ProducesResponseType(typeof(IEnumerable<ConflictDetectedEvent>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ConflictDetectedEvent>>> GetDroneConflicts(string droneId)
    {
        var drone = _droneStateStore.GetDrone(droneId);
        if (drone == null)
            return NotFound($"Drone {droneId} not found");

        var allDrones = _droneStateStore.GetAllDrones().Where(d => d.Id != droneId).ToList();
        var conflicts = await _conflictDetector.DetectDroneConflictsAsync(drone, allDrones);
        return Ok(conflicts);
    }

    /// <summary>
    /// 获取冲突统计
    /// </summary>
    /// <returns>各等级冲突数量</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetConflictStats()
    {
        if (_conflictDetector is not TimeWindowConflictDetector detector)
            return Ok(new { total = 0, critical = 0, high = 0, medium = 0, low = 0 });

        var conflicts = detector.GetActiveConflicts().ToList();

        return Ok(new
        {
            total = conflicts.Count,
            critical = conflicts.Count(c => c.Severity == ConflictSeverity.Critical),
            high = conflicts.Count(c => c.Severity == ConflictSeverity.High),
            medium = conflicts.Count(c => c.Severity == ConflictSeverity.Medium),
            low = conflicts.Count(c => c.Severity == ConflictSeverity.Low)
        });
    }

    /// <summary>
    /// 获取或设置安全距离
    /// </summary>
    /// <param name="distance">安全距离 (米)</param>
    /// <returns>当前配置</returns>
    [HttpPut("config/safety-distance")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult SetSafetyDistance([FromBody] double distance)
    {
        if (distance <= 0)
            return BadRequest("Safety distance must be greater than 0");

        _conflictDetector.SafetyDistance = distance;
        return Ok(new { safetyDistance = _conflictDetector.SafetyDistance });
    }

    /// <summary>
    /// 获取或设置预测时间范围
    /// </summary>
    /// <param name="horizon">预测时间范围 (秒)</param>
    /// <returns>当前配置</returns>
    [HttpPut("config/prediction-horizon")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult SetPredictionHorizon([FromBody] double horizon)
    {
        if (horizon <= 0)
            return BadRequest("Prediction horizon must be greater than 0");

        _conflictDetector.PredictionHorizon = horizon;
        return Ok(new { predictionHorizon = _conflictDetector.PredictionHorizon });
    }
}

/// <summary>
/// 系统状态API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    private readonly IDroneStateStore _droneStateStore;
    private readonly IConflictDetector _conflictDetector;
    private readonly ITrajectoryService _trajectoryService;
    private readonly IConfiguration _configuration;

    public SystemController(
        IDroneStateStore droneStateStore,
        IConflictDetector conflictDetector,
        ITrajectoryService trajectoryService,
        IConfiguration configuration)
    {
        _droneStateStore = droneStateStore;
        _conflictDetector = conflictDetector;
        _trajectoryService = trajectoryService;
        _configuration = configuration;
    }

    /// <summary>
    /// 获取系统健康状态
    /// </summary>
    /// <returns>健康状态</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow,
            version = "1.0.0"
        });
    }

    /// <summary>
    /// 获取系统概览
    /// </summary>
    /// <returns>系统概览信息</returns>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetOverview()
    {
        int conflictCount = 0;
        if (_conflictDetector is TimeWindowConflictDetector detector)
        {
            conflictCount = detector.GetActiveConflicts().Count;
        }

        return Ok(new
        {
            droneCount = _droneStateStore.Count,
            trackedTrajectories = _trajectoryService.TrackedDroneCount,
            activeConflicts = conflictCount,
            safetyDistance = _conflictDetector.SafetyDistance,
            predictionHorizon = _conflictDetector.PredictionHorizon,
            systemTime = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// 获取系统配置
    /// </summary>
    /// <returns>配置信息</returns>
    [HttpGet("config")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetConfig()
    {
        return Ok(new
        {
            detection = new
            {
                intervalMs = _configuration.GetValue("Detection:IntervalMs", 200),
                safetyDistanceMeters = _conflictDetector.SafetyDistance,
                predictionHorizonSeconds = _conflictDetector.PredictionHorizon
            },
            simulation = new
            {
                droneCount = _configuration.GetValue("Simulation:DroneCount", 100),
                intervalMs = _configuration.GetValue("Simulation:IntervalMs", 100),
                centerLongitude = _configuration.GetValue("Simulation:CenterLongitude", 116.3972),
                centerLatitude = _configuration.GetValue("Simulation:CenterLatitude", 39.9075),
                baseAltitude = _configuration.GetValue("Simulation:BaseAltitude", 100.0)
            },
            pipeline = new
            {
                capacity = _configuration.GetValue("Pipeline:Capacity", 10000)
            }
        });
    }
}
