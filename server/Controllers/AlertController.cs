using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;

namespace Termometriya.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertController : ControllerBase
{
    private readonly AppDbContext _db;

    public AlertController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAlerts([FromQuery] bool activeOnly = false, [FromQuery] int? siloId = null, [FromQuery] int? take = null)
    {
        var query = _db.AlertEvents
            .Include(a => a.Silo).ThenInclude(s => s.Line)
            .AsQueryable();

        if (activeOnly) query = query.Where(a => a.IsActive);
        if (siloId.HasValue) query = query.Where(a => a.SiloId == siloId);

        query = query.OrderByDescending(a => a.Timestamp);

        if (take.HasValue) query = query.Take(take.Value);

        var alerts = await query.Select(a => new
        {
            a.Id,
            a.SiloId,
            a.ThermopendantId,
            a.AlertType,
            a.PointIndex,
            a.Value,
            a.Threshold,
            a.Message,
            a.Timestamp,
            a.IsActive,
            a.AcknowledgedAt,
            a.ResolvedAt,
            SiloNumber = a.Silo.Number,
            LineName = a.Silo.Line.Name
        }).ToListAsync();

        return Ok(alerts);
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<IActionResult> Acknowledge(long id, [FromBody] AcknowledgeDto? dto)
    {
        var alert = await _db.AlertEvents.FindAsync(id);
        if (alert == null) return NotFound();

        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedBy = dto?.User ?? "operator";
        await _db.SaveChangesAsync();

        return Ok(new { alert.Id, alert.AcknowledgedAt });
    }

    [HttpPost("acknowledge-all")]
    public async Task<IActionResult> AcknowledgeAll([FromBody] AcknowledgeDto? dto)
    {
        var active = await _db.AlertEvents.Where(a => a.IsActive && a.AcknowledgedAt == null).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var a in active)
        {
            a.AcknowledgedAt = now;
            a.AcknowledgedBy = dto?.User ?? "operator";
        }
        await _db.SaveChangesAsync();

        return Ok(new { count = active.Count });
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> Resolve(long id)
    {
        var alert = await _db.AlertEvents.FindAsync(id);
        if (alert == null) return NotFound();

        alert.IsActive = false;
        alert.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { alert.Id, alert.ResolvedAt });
    }

    [HttpPost("resolve-all")]
    public async Task<IActionResult> ResolveAll()
    {
        var active = await _db.AlertEvents.Where(a => a.IsActive).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var a in active)
        {
            a.IsActive = false;
            a.ResolvedAt = now;
        }
        await _db.SaveChangesAsync();

        return Ok(new { count = active.Count });
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var config = await _db.AlertConfigs.FirstOrDefaultAsync();
        return Ok(config);
    }

    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] AlertConfigDto dto)
    {
        var config = await _db.AlertConfigs.FirstOrDefaultAsync();
        if (config == null)
        {
            config = new Models.AlertConfig();
            _db.AlertConfigs.Add(config);
        }

        config.SoundEnabled = dto.SoundEnabled;
        config.EmailEnabled = dto.EmailEnabled;
        config.SmtpHost = dto.SmtpHost ?? "";
        config.SmtpPort = dto.SmtpPort;
        config.SmtpUser = dto.SmtpUser ?? "";
        config.SmtpPass = dto.SmtpPass ?? "";
        config.EmailRecipients = dto.EmailRecipients ?? "";

        await _db.SaveChangesAsync();
        return Ok(config);
    }
}

public class AcknowledgeDto
{
    public string? User { get; set; }
}

public class AlertConfigDto
{
    public bool SoundEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 25;
    public string? SmtpUser { get; set; }
    public string? SmtpPass { get; set; }
    public string? EmailRecipients { get; set; }
}
