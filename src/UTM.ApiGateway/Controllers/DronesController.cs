using Microsoft.AspNetCore.Mvc;
using UTM.SharedKernel.Models;
using UTM.TelemetryService.Services;
using UTM.TelemetryService.Simulation;

namespace UTM.ApiGateway.Controllers;

/// <summary>
/// 无人机管理API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DronesController : ControllerBase
{
    private readonly IDroneStateStore _droneStateStore;
    private readonly DroneSimulator _simulator;
    private readonly ILogger<DronesController> _logger;

    public DronesController(
        IDroneStateStore droneStateStore,
        DroneSimulator simulator,
        ILogger<DronesController> logger)
    {
        _droneStateStore = droneStateStore;
        _simulator = simulator;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有无人机列表
    /// </summary>
    /// <returns>无人机列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Drone>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<Drone>> GetAll()
    {
        var drones = _droneStateStore.GetAllDrones().ToList();
        return Ok(drones);
    }

    /// <summary>
    /// 获取指定无人机详情
    /// </summary>
    /// <param name="id">无人机ID</param>
    /// <returns>无人机详细信息</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Drone), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Drone> GetById(string id)
    {
        var drone = _droneStateStore.GetDrone(id);
        if (drone == null)
            return NotFound($"Drone {id} not found");

        return Ok(drone);
    }

    /// <summary>
    /// 获取指定区域内的无人机
    /// </summary>
    /// <param name="minLon">最小经度</param>
    /// <param name="minLat">最小纬度</param>
    /// <param name="maxLon">最大经度</param>
    /// <param name="maxLat">最大纬度</param>
    /// <returns>区域内的无人机列表</returns>
    [HttpGet("area")]
    [ProducesResponseType(typeof(IEnumerable<Drone>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<Drone>> GetInArea(
        [FromQuery] double minLon,
        [FromQuery] double minLat,
        [FromQuery] double maxLon,
        [FromQuery] double maxLat)
    {
        var drones = _droneStateStore.GetDronesInArea(minLon, minLat, maxLon, maxLat).ToList();
        return Ok(drones);
    }

    /// <summary>
    /// 获取无人机状态统计
    /// </summary>
    /// <returns>各状态的无人机数量统计</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(Dictionary<string, int>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, int>> GetStats()
    {
        var drones = _droneStateStore.GetAllDrones().ToList();
        var stats = drones
            .GroupBy(d => d.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        return Ok(stats);
    }

    /// <summary>
    /// 设置无人机速度
    /// </summary>
    /// <param name="id">无人机ID</param>
    /// <param name="velocity">新的速度</param>
    /// <returns>操作结果</returns>
    [HttpPut("{id}/velocity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult SetVelocity(string id, [FromBody] Velocity3D velocity)
    {
        var drone = _droneStateStore.GetDrone(id);
        if (drone == null)
            return NotFound($"Drone {id} not found");

        _simulator.SetVelocity(id, velocity);
        return Ok(new { message = "Velocity updated" });
    }

    /// <summary>
    /// 获取无人机总数
    /// </summary>
    /// <returns>无人机数量</returns>
    [HttpGet("count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public ActionResult<int> GetCount()
    {
        return Ok(_droneStateStore.Count);
    }
}
