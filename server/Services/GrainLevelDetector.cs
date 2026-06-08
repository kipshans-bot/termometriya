using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Models;

namespace Termometriya.Server.Services;

public class GrainLevelDetector
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GrainLevelDetector> _logger;
    private const double VarianceThreshold = 2.0;
    private static readonly TimeSpan AnalysisWindow = TimeSpan.FromHours(48);
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromHours(1);

    public GrainLevelDetector(IServiceScopeFactory scopeFactory, ILogger<GrainLevelDetector> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task DetectForAllSilosAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var cutoff = now - AnalysisWindow;

        var silos = await db.Silos
            .Include(s => s.Thermopendants.Where(t => t.IsActive))
            .Where(s => s.GrainLevelLastChecked == null || s.GrainLevelLastChecked < now - RecheckInterval)
            .ToListAsync();

        foreach (var silo in silos)
        {
            try
            {
                await DetectForSiloAsync(db, silo, cutoff);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Grain level detection failed for silo {SiloId}", silo.Id);
            }
        }
    }

    private async Task DetectForSiloAsync(AppDbContext db, Silo silo, DateTime cutoff)
    {
        var activePendants = silo.Thermopendants.Where(t => t.IsActive).ToList();
        if (activePendants.Count == 0) return;

        var pendantIds = activePendants.Select(p => p.Id).ToList();

        var recentReadings = await db.SensorReadings
            .Where(r => pendantIds.Contains(r.ThermopendantId)
                && r.IsValid
                && r.Timestamp >= cutoff)
            .Select(r => new { r.ThermopendantId, r.PointIndex, r.Temperature, r.Timestamp })
            .ToListAsync();

        if (recentReadings.Count < 50) return;

        var pointVariances = recentReadings
            .GroupBy(r => new { r.ThermopendantId, r.PointIndex })
            .Select(g => new
            {
                ThermopendantId = g.Key.ThermopendantId,
                PointIndex = g.Key.PointIndex,
                Variance = ComputeVariance(g.Select(x => (double)x.Temperature))
            })
            .GroupBy(x => x.PointIndex)
            .Select(g => new
            {
                PointIndex = g.Key,
                AvgVariance = g.Average(x => x.Variance)
            })
            .OrderBy(x => x.PointIndex)
            .ToList();

        if (pointVariances.Count < 3) return;

        int? grainLevel = null;
        bool foundTransition = false;
        foreach (var pv in pointVariances)
        {
            if (pv.AvgVariance >= VarianceThreshold && !foundTransition)
            {
                grainLevel = pv.PointIndex;
                foundTransition = true;
            }
        }

        if (grainLevel.HasValue)
        {
            silo.GrainLevelPointIndex = grainLevel.Value;
            _logger.LogInformation("Silo {SiloId}: grain level detected at point index {Index} (avg variance {V:F2})",
                silo.Id, grainLevel.Value,
                pointVariances.FirstOrDefault(p => p.PointIndex == grainLevel.Value)?.AvgVariance ?? 0);
        }
        else
        {
            silo.GrainLevelPointIndex = null;
        }

        silo.GrainLevelLastChecked = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static double ComputeVariance(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count < 2) return double.MaxValue;
        double avg = list.Average();
        return list.Sum(v => (v - avg) * (v - avg)) / (list.Count - 1);
    }
}
