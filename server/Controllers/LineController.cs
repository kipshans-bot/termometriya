using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;

namespace Termometriya.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LineController : ControllerBase
{
    private readonly AppDbContext _db;

    public LineController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var lines = await _db.ElevatorLines
            .Include(l => l.Silos).ThenInclude(s => s.Culture)
            .Include(l => l.Silos).ThenInclude(s => s.Thermopendants)
            .OrderBy(l => l.DisplayOrder)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.DisplayOrder,
                Silos = l.Silos.OrderBy(s => s.Number).Select(s => new
                {
                    s.Id,
                    s.Number,
                    s.FillLevel,
                    s.Capacity,
                    s.CultureId,
                    CultureName = s.Culture.Name,
                    PendantCount = s.Thermopendants.Count(t => t.IsActive)
                })
            })
            .ToListAsync();

        return Ok(lines);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] LineUpdateDto dto)
    {
        var line = await _db.ElevatorLines.FindAsync(id);
        if (line == null) return NotFound();
        line.Name = dto.Name;
        await _db.SaveChangesAsync();
        return Ok(line);
    }
}

public class LineUpdateDto
{
    public string Name { get; set; } = string.Empty;
}
