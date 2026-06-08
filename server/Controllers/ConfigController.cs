using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Termometriya.Server.Models;
using Termometriya.Server.Services;
using Termometriya.Server.Services.DqtService;

namespace Termometriya.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly ModbusTcpSimulator _simulator;
    private readonly ElevatorConfigService _elevatorConfig;

    public ConfigController(ModbusTcpSimulator simulator, ElevatorConfigService elevatorConfig)
    {
        _simulator = simulator;
        _elevatorConfig = elevatorConfig;
    }

    [HttpGet("hardware")]
    public IActionResult GetHardwareConfig()
    {
        return Ok(_simulator.GetConfig());
    }

    [HttpPut("hardware")]
    public async Task<IActionResult> UpdateHardwareConfig([FromBody] Bkt12HardwareConfig config)
    {
        await _simulator.SaveConfigAsync(config);
        return Ok(new { status = "saved" });
    }

    [HttpGet("elevator")]
    public async Task<IActionResult> GetElevatorConfig()
    {
        var config = await _elevatorConfig.LoadFromDbAsync();
        return Ok(config);
    }

    [HttpPut("elevator")]
    public async Task<IActionResult> UpdateElevatorConfig([FromBody] ElevatorConfig config)
    {
        await _elevatorConfig.SaveAsync(config);
        await _elevatorConfig.SyncToDbAsync(config);
        return Ok(new { status = "saved" });
    }
}
