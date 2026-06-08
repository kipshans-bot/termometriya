using Microsoft.AspNetCore.Mvc;
using Termometriya.Server.Data;
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
    private readonly AppDbContext _db;

    public ConfigController(ModbusTcpSimulator simulator, ElevatorConfigService elevatorConfig, AppDbContext db)
    {
        _simulator = simulator;
        _elevatorConfig = elevatorConfig;
        _db = db;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        return Ok(_simulator.GetConfig());
    }

    [HttpPut]
    public async Task<IActionResult> UpdateConfig([FromBody] ThermometryConfig config)
    {
        await _simulator.SaveConfigAsync(config);
        await _elevatorConfig.SyncToDbAsync(config, _db);
        return Ok(new { status = "saved" });
    }
}
