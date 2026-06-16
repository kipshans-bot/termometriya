using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Models;
using Termometriya.Server.Services;

namespace Termometriya.Server.Controllers;

public class SiloPointData
{
    public int Index { get; set; }
    public double? Temp { get; set; }
    public double? Humidity { get; set; }
    public bool IsValid { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class SiloController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ElevatorConfigService _config;

    public SiloController(AppDbContext db, ElevatorConfigService config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var silos = await _db.Silos
            .Include(s => s.Line)
            .Include(s => s.Culture)
            .Select(s => new
            {
                s.Id,
                s.Number,
                s.LineId,
                s.FillLevel,
                s.Capacity,
                s.CultureId,
                s.GrainLevelPointIndex,
                LineName = s.Line.Name,
                CultureName = s.Culture.Name,
                PendantCount = s.Thermopendants.Count(t => t.IsActive)
            })
            .ToListAsync();

        var siloIds = silos.Select(s => s.Id).ToList();
        var lastReadings = await _db.SensorReadings
            .Where(r => siloIds.Contains(r.SiloId) && r.IsValid)
            .GroupBy(r => new { r.SiloId, r.ThermopendantId, r.PointIndex })
            .Select(g => g.OrderByDescending(r => r.Timestamp).First())
            .ToListAsync();

        var alerts = await _db.AlertEvents.Where(a => a.IsActive && siloIds.Contains(a.SiloId)).ToListAsync();

        var result = silos.Select(s => new
        {
            s.Id,
            s.Number,
            s.LineId,
            s.FillLevel,
            s.Capacity,
            s.CultureId,
            s.LineName,
            s.CultureName,
            s.PendantCount,
            MaxTemp = lastReadings.Where(r => r.SiloId == s.Id).Select(r => (double?)r.Temperature).Max() ?? 0,
            AvgTemp = lastReadings.Where(r => r.SiloId == s.Id).Select(r => (double?)r.Temperature).DefaultIfEmpty().Average() ?? 0,
            HasActiveAlert = alerts.Any(a => a.SiloId == s.Id),
            AlertLevel = alerts.Where(a => a.SiloId == s.Id).Select(a => (int)a.AlertType).DefaultIfEmpty(0).Max()
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var silo = await _db.Silos
            .Include(s => s.Line)
            .Include(s => s.Culture)
            .Include(s => s.Thermopendants)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (silo == null) return NotFound();

        var latestReadings = await _db.SensorReadings
            .Where(r => r.SiloId == id)
            .GroupBy(r => new { r.ThermopendantId, r.PointIndex })
            .Select(g => g.OrderByDescending(r => r.Timestamp).First())
            .ToListAsync();

        var activeAlerts = await _db.AlertEvents
            .Where(a => a.SiloId == id && a.IsActive)
            .ToListAsync();

        var result = new
        {
            silo.Id,
            silo.Number,
            silo.LineId,
            silo.FillLevel,
            silo.Capacity,
            silo.CultureId,
            silo.GrainLevelPointIndex,
            LineName = silo.Line.Name,
            CultureName = silo.Culture.Name,
            Pendants = silo.Thermopendants.Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).Select(t =>
            {
                var tReadings = latestReadings.Where(r => r.ThermopendantId == t.Id).ToList();
                return new
                {
                    t.Id,
                    t.PositionIndex,
                    t.PointCount,
                    t.DisplayOrder,
                    IsCentral = t.IsCentral,
                    Points = Enumerable.Range(0, t.PointCount).Select(idx => (object)(
                        tReadings.FirstOrDefault(p => p.PointIndex == idx) is var r && r != null
                            ? new SiloPointData { Index = idx, Temp = Math.Round(r.Temperature, 1), Humidity = r.Humidity.HasValue ? Math.Round(r.Humidity.Value, 1) : null, IsValid = r.IsValid }
                            : new SiloPointData { Index = idx, Temp = null, Humidity = null, IsValid = false }
                    ))
                };
            }),
            Alerts = activeAlerts.Select(a => new
            {
                a.Id,
                a.AlertType,
                a.ThermopendantId,
                a.PointIndex,
                a.Value,
                a.Threshold,
                a.Message,
                a.Timestamp
            })
        };

        return Ok(result);
    }

    [HttpPut("{id}/configure")]
    public async Task<IActionResult> Configure(int id, [FromBody] SiloConfigureDto dto)
    {
        var silo = await _db.Silos
            .Include(s => s.Thermopendants)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (silo == null) return NotFound();

        if (dto.CultureId.HasValue)
        {
            var culture = await _db.Cultures.FindAsync(dto.CultureId.Value);
            if (culture == null) return BadRequest("Culture not found");
            silo.CultureId = dto.CultureId.Value;
        }

        if (dto.FillLevel.HasValue) silo.FillLevel = dto.FillLevel.Value;
        if (dto.Pendants != null)
        {
            var existing = silo.Thermopendants.Where(t => t.IsActive).ToList();

            foreach (var existingT in existing)
            {
                var match = dto.Pendants.FirstOrDefault(p => p.PositionIndex == existingT.PositionIndex);
                if (match == null)
                    existingT.IsActive = false;
            }

            for (int i = 0; i < dto.Pendants.Count; i++)
            {
                var cfg = dto.Pendants[i];
                var match = existing.FirstOrDefault(t => t.IsActive && t.PositionIndex == cfg.PositionIndex);
                if (match != null)
                {
                    match.PointCount = cfg.PointCount;
                    match.DisplayOrder = i;
                }
                else
                {
                    _db.Thermopendants.Add(new Thermopendant
                    {
                        SiloId = id,
                        PositionIndex = cfg.PositionIndex,
                        PointCount = cfg.PointCount,
                        DisplayOrder = i,
                        IsActive = true
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { status = "ok" });
    }

    [HttpGet("{id}/readings")]
    public async Task<IActionResult> GetReadings(int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _db.SensorReadings.Where(r => r.SiloId == id);
        if (from.HasValue) query = query.Where(r => r.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(r => r.Timestamp <= to.Value);

        var readings = await query
            .OrderByDescending(r => r.Timestamp)
            .Take(5000)
            .Select(r => new
            {
                r.ThermopendantId,
                r.PointIndex,
                r.Temperature,
                r.Humidity,
                r.IsValid,
                r.Timestamp
            })
            .ToListAsync();

        return Ok(readings);
    }

    [HttpGet("{id}/delta")]
    public async Task<IActionResult> GetDelta(int id, [FromQuery] int? hours)
    {
        var cfg = await _config.LoadAsync();
        var actualHours = hours ?? cfg.DeltaHours;

        var silo = await _db.Silos
            .Include(s => s.Thermopendants.Where(t => t.IsActive))
            .FirstOrDefaultAsync(s => s.Id == id);
        if (silo == null) return NotFound();

        var threshold = DateTime.UtcNow.AddHours(-actualHours);

        var stats = await _db.SensorReadings
            .Where(r => r.SiloId == id && r.IsValid && r.Timestamp >= threshold)
            .GroupBy(r => new { r.ThermopendantId, r.PointIndex })
            .Select(g => new
            {
                g.Key.ThermopendantId,
                g.Key.PointIndex,
                MinTemp = g.Min(r => r.Temperature),
                MaxTemp = g.Max(r => r.Temperature)
            })
            .ToListAsync();

        var statsLookup = stats.ToDictionary(s => (s.ThermopendantId, s.PointIndex));

        var result = silo.Thermopendants.Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).Select(t =>
        {
            var points = Enumerable.Range(0, t.PointCount).Select(idx =>
            {
                var key = (t.Id, idx);
                double? minTemp = null, maxTemp = null, delta = null;
                if (statsLookup.TryGetValue(key, out var s))
                {
                    minTemp = Math.Round(s.MinTemp, 1);
                    maxTemp = Math.Round(s.MaxTemp, 1);
                    delta = Math.Round(s.MaxTemp - s.MinTemp, 1);
                }
                return new { PointIndex = idx, Delta = delta, MinTemp = minTemp, MaxTemp = maxTemp };
            }).ToList();
            return new
            {
                t.Id,
                t.PositionIndex,
                t.PointCount,
                t.DisplayOrder,
                t.IsCentral,
                Points = points
            };
        }).ToList();

        return Ok(new { SiloId = id, Hours = actualHours, Pendants = result });
    }
}

public class SiloConfigureDto
{
    public int? CultureId { get; set; }
    public double? FillLevel { get; set; }
    public List<PendantConfigDto>? Pendants { get; set; }
    public List<int>? PendantPointCounts { get; set; }
}

public class PendantConfigDto
{
    public int PositionIndex { get; set; }
    public int PointCount { get; set; } = 30;
}
