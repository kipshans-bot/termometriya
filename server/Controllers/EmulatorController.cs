using Microsoft.AspNetCore.Mvc;
using Termometriya.Server.Services.DqtService;

namespace Termometriya.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmulatorController : ControllerBase
{
    private readonly ModbusTcpSimulator _simulator;

    public EmulatorController(ModbusTcpSimulator simulator) => _simulator = simulator;

    [HttpGet("scenarios")]
    public IActionResult GetScenarios()
    {
        return Ok(new[] { "normal", "heating", "selfheating", "critical" });
    }

    [HttpPost("scenario/{siloId}")]
    public IActionResult SetScenario(int siloId, [FromBody] ScenarioDto dto)
    {
        _simulator.SetScenario(siloId, dto.Scenario);
        return Ok(new { siloId, scenario = dto.Scenario });
    }

    [HttpPost("reset")]
    public IActionResult Reset()
    {
        _simulator.ResetAll();
        return Ok(new { status = "reset" });
    }

    [HttpGet("hardware")]
    public IActionResult GetHardware()
    {
        var config = _simulator.GetConfig();
        return Ok(config);
    }
}

public class ScenarioDto
{
    public string Scenario { get; set; } = "normal";
}
