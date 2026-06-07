using Microsoft.AspNetCore.Mvc;
using UTM.SharedKernel.Models;
using UTM.TelemetryService.Pipeline;
using UTM.TelemetryService.Services;

namespace UTM.ApiGateway.Controllers;

/// <summary>
/// 遥测数据API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TelemetryController : ControllerBase
{
    private readonly ITelemetryPipeline _pipeline;
    private readonly ITrajectoryService _trajectoryService;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(
        ITelemetryPipeline pipeline,
        ITrajectoryService trajectoryService,
        ILogger<TelemetryController> logger)
    {
        _pipeline = pipeline;
        _trajectoryService = trajectoryService;
        _logger = logger;
    }

    /// <summary>
    /// 接收遥测数据
    /// </summary>
    /// <param name="telemetry">遥测数据</param>
    /// <returns>接收结果</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveTelemetry([FromBody] TelemetryData telemetry)
    {
        if (string.IsNullOrEmpty(telemetry.DroneId))
            return BadRequest("DroneId is required");

        try
        {
            await _pipeline.WriteAsync(telemetry);
            return Accepted(new { message = "Telemetry received", droneId = telemetry.DroneId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving telemetry");
            return StatusCode(500, new { error = "Failed to process telemetry" });
        }
    }

    /// <summary>
    /// 批量接收遥测数据
    /// </summary>
    /// <param name="telemetryList">遥测数据列表</param>
    /// <returns>接收结果</returns>
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveBatch([FromBody] List<TelemetryData> telemetryList)
    {
        if (telemetryList == null || telemetryList.Count == 0)
            return BadRequest("No telemetry data provided");

        try
        {
            await _pipeline.WriteBatchAsync(telemetryList);
            return Accepted(new { message = "Batch telemetry received", count = telemetryList.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving batch telemetry");
            return StatusCode(500, new { error = "Failed to process batch" });
        }
    }

    /// <summary>
    /// 获取管道状态
    /// </summary>
    /// <returns>管道状态信息</returns>
    [HttpGet("pipeline/status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetPipelineStatus()
    {
        return Ok(new
        {
            queueLength = _pipeline.QueueLength,
            totalProcessed = _pipeline.TotalProcessed
        });
    }
}

/// <summary>
/// 轨迹查询API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TrajectoryController : ControllerBase
{
    private readonly ITrajectoryService _trajectoryService;
    private readonly ILogger<TrajectoryController> _logger;

    public TrajectoryController(
        ITrajectoryService trajectoryService,
        ILogger<TrajectoryController> logger)
    {
        _trajectoryService = trajectoryService;
        _logger = logger;
    }

    /// <summary>
    /// 获取无人机历史轨迹
    /// </summary>
    /// <param name="droneId">无人机ID</param>
    /// <param name="seconds">时间范围 (秒)，默认300秒</param>
    /// <returns>轨迹点列表</returns>
    [HttpGet("{droneId}")]
    [ProducesResponseType(typeof(IEnumerable<TrajectoryPoint>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IEnumerable<TrajectoryPoint>> GetTrajectory(
        string droneId,
        [FromQuery] int seconds = 300)
    {
        var trajectory = _trajectoryService.GetTrajectory(droneId, TimeSpan.FromSeconds(seconds));
        if (trajectory.Count == 0)
            return NotFound($"No trajectory data found for drone {droneId}");

        return Ok(trajectory);
    }

    /// <summary>
    /// 预测无人机未来轨迹
    /// </summary>
    /// <param name="droneId">无人机ID</param>
    /// <param name="horizon">预测时间范围 (秒)，默认60秒</param>
    /// <param name="step">时间步长 (秒)，默认1秒</param>
    /// <returns>预测轨迹点列表</returns>
    [HttpGet("{droneId}/predict")]
    [ProducesResponseType(typeof(IEnumerable<TrajectoryPoint>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TrajectoryPoint>> PredictTrajectory(
        string droneId,
        [FromQuery] int horizon = 60,
        [FromQuery] double step = 1.0)
    {
        var predicted = _trajectoryService.PredictTrajectory(droneId, horizon, step);
        return Ok(predicted);
    }

    /// <summary>
    /// 获取跟踪的无人机数量
    /// </summary>
    /// <returns>跟踪数量</returns>
    [HttpGet("tracked/count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public ActionResult<int> GetTrackedCount()
    {
        return Ok(_trajectoryService.TrackedDroneCount);
    }
}
