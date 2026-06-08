using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;

namespace Termometriya.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CultureController : ControllerBase
{
    private readonly AppDbContext _db;

    public CultureController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cultures = await _db.Cultures.ToListAsync();
        return Ok(cultures);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CultureUpdateDto dto)
    {
        var culture = await _db.Cultures.FindAsync(id);
        if (culture == null) return NotFound();

        culture.NormTemp = dto.NormTemp;
        culture.WarnTemp = dto.WarnTemp;
        culture.CriticalTemp = dto.CriticalTemp;
        culture.GradientWarn = dto.GradientWarn;
        culture.GradientCritical = dto.GradientCritical;
        culture.DeviationThreshold = dto.DeviationThreshold;
        culture.HighTempThreshold = dto.HighTempThreshold;
        culture.HighTempGradient = dto.HighTempGradient;
        culture.CriticalHighTemp50 = dto.CriticalHighTemp50;
        culture.SoundEnabled = dto.SoundEnabled;
        culture.EmailEnabled = dto.EmailEnabled;
        culture.EmailRecipients = dto.EmailRecipients ?? "";

        await _db.SaveChangesAsync();
        return Ok(culture);
    }
}

public class CultureUpdateDto
{
    public double NormTemp { get; set; }
    public double WarnTemp { get; set; }
    public double CriticalTemp { get; set; }
    public double GradientWarn { get; set; }
    public double GradientCritical { get; set; }
    public double DeviationThreshold { get; set; }
    public double HighTempThreshold { get; set; }
    public double HighTempGradient { get; set; }
    public double CriticalHighTemp50 { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; }
    public string? EmailRecipients { get; set; }
}
