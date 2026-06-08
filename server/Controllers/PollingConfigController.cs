using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Models;

namespace Termometriya.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PollingConfigController : ControllerBase
{
    private readonly AppDbContext _db;

    public PollingConfigController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var config = await _db.PollingConfigs.FirstOrDefaultAsync();
        return Ok(config ?? new PollingConfig());
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] PollingConfig dto)
    {
        var config = await _db.PollingConfigs.FirstOrDefaultAsync();
        if (config == null)
        {
            config = new PollingConfig();
            _db.PollingConfigs.Add(config);
        }
        config.NormalIntervalSec = dto.NormalIntervalSec;
        config.ElevatedIntervalSec = dto.ElevatedIntervalSec;
        await _db.SaveChangesAsync();
        return Ok(config);
    }
}
