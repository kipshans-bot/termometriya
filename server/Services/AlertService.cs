using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Models;

namespace Termometriya.Server.Services;

public class AlertService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AlertService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<List<AlertEvent>> EvaluateAsync(List<SensorReading> readings)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var siloIds = readings.Select(r => r.SiloId).Distinct().ToList();
        var cultures = await db.Cultures.ToListAsync();
        var existingAlerts = await db.AlertEvents
            .Where(a => a.IsActive && siloIds.Contains(a.SiloId))
            .ToListAsync();

        var result = new List<AlertEvent>();
        var modifiedExisting = false;

        foreach (var group in readings.GroupBy(r => r.SiloId))
        {
            var siloReadings = group.ToList();
            var silo = siloReadings.First().Silo;
            var culture = cultures.FirstOrDefault(c => c.Id == silo.CultureId);
            if (culture == null) continue;

            var grainIdx = silo.GrainLevelPointIndex;
            var grainReadings = grainIdx.HasValue
                ? siloReadings.Where(r => r.IsValid && r.PointIndex < grainIdx.Value).ToList()
                : siloReadings.Where(r => r.IsValid).ToList();
            double avgTemp = grainReadings.Any() ? grainReadings.Average(r => r.Temperature) : 0;

            foreach (var reading in siloReadings)
            {
                if (!reading.IsValid)
                {
                    CheckAlert(result, existingAlerts, reading, AlertType.SensorFault,
                        true, 0,
                        $"{culture.Name}: Точка {reading.PointIndex} — неисправен датчик");
                    continue;
                }

                var existingFault = existingAlerts.FirstOrDefault(a =>
                    a.SiloId == reading.SiloId
                    && a.ThermopendantId == reading.ThermopendantId
                    && a.PointIndex == reading.PointIndex
                    && a.AlertType == AlertType.SensorFault
                    && a.IsActive);
                if (existingFault != null)
                {
                    existingFault.IsActive = false;
                    existingFault.ResolvedAt = DateTime.UtcNow;
                    modifiedExisting = true;
                }

                if (silo.GrainLevelPointIndex.HasValue && reading.PointIndex >= silo.GrainLevelPointIndex.Value)
                    continue;

                var t = reading.Temperature;

                CheckAlert(result, existingAlerts, reading, AlertType.Critical,
                    t >= culture.CriticalHighTemp50, culture.CriticalHighTemp50,
                    $"{culture.Name}: Точка {reading.PointIndex} — {t:F1}°C (необратимая порча)");

                CheckAlert(result, existingAlerts, reading, AlertType.Critical,
                    t >= culture.CriticalTemp, culture.CriticalTemp,
                    $"{culture.Name}: Точка {reading.PointIndex} — {t:F1}°C (критическая)");

                CheckAlert(result, existingAlerts, reading, AlertType.Warning,
                    t >= culture.WarnTemp, culture.WarnTemp,
                    $"{culture.Name}: Точка {reading.PointIndex} — {t:F1}°C (выше нормы)");

                if (avgTemp > 0)
                {
                    double deviation = Math.Abs(t - avgTemp);
                    CheckAlert(result, existingAlerts, reading, AlertType.DeviationWarning,
                        deviation >= culture.DeviationThreshold, culture.DeviationThreshold,
                        $"{culture.Name}: Точка {reading.PointIndex} — девиация {deviation:F1}°C от средней");
                }

                var prevReading = await db.SensorReadings
                    .Where(r => r.ThermopendantId == reading.ThermopendantId && r.PointIndex == reading.PointIndex
                        && r.Timestamp < reading.Timestamp)
                    .OrderByDescending(r => r.Timestamp)
                    .FirstOrDefaultAsync();

                if (prevReading != null)
                {
                    var hoursDiff = (reading.Timestamp - prevReading.Timestamp).TotalHours;
                    if (hoursDiff >= 0.25)
                    {
                        double gradient = (t - prevReading.Temperature) / hoursDiff * 24;
                        double threshold = t > culture.HighTempThreshold
                            ? culture.HighTempGradient : culture.GradientCritical;

                        CheckAlert(result, existingAlerts, reading, AlertType.GradientCritical,
                            gradient >= threshold, threshold,
                            $"{culture.Name}: Точка {reading.PointIndex} — градиент {gradient:F1}°C/сут");

                        CheckAlert(result, existingAlerts, reading, AlertType.GradientWarning,
                            gradient >= culture.GradientWarn, culture.GradientWarn,
                            $"{culture.Name}: Точка {reading.PointIndex} — градиент {gradient:F1}°C/сут (начальная стадия)");
                    }
                }
            }
        }

        if (result.Count > 0 || modifiedExisting)
        {
            if (result.Count > 0)
                db.AlertEvents.AddRange(result);
            await db.SaveChangesAsync();
        }

        return result;
    }

    public async Task ResolveInactiveAlerts(List<SensorReading> readings)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var siloIds = readings.Select(r => r.SiloId).Distinct().ToList();
        var cultures = await db.Cultures.ToListAsync();
        var activeAlerts = await db.AlertEvents
            .Where(a => a.IsActive && siloIds.Contains(a.SiloId)
                && a.AlertType != AlertType.SensorFault)
            .ToListAsync();

        foreach (var siloGroup in readings.GroupBy(r => r.SiloId))
        {
            var silo = siloGroup.First().Silo;
            var culture = cultures.FirstOrDefault(c => c.Id == silo.CultureId);
            if (culture == null) continue;

            var grainIdx = silo.GrainLevelPointIndex;
            var validReadings = grainIdx.HasValue
                ? siloGroup.Where(r => r.IsValid && r.PointIndex < grainIdx.Value).ToList()
                : siloGroup.Where(r => r.IsValid).ToList();
            if (validReadings.Count == 0) continue;

            var maxTemp = validReadings.Max(r => r.Temperature);
            double hysteresis = 2.0;
            double resolveThreshold = Math.Max(culture.NormTemp, culture.WarnTemp - hysteresis);

            var toResolve = activeAlerts
                .Where(a => a.SiloId == silo.Id && maxTemp <= resolveThreshold)
                .ToList();

            foreach (var alert in toResolve)
            {
                alert.IsActive = false;
                alert.ResolvedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
    }

    private void CheckAlert(List<AlertEvent> result, List<AlertEvent> existingAlerts,
        SensorReading reading, AlertType type, bool condition, double threshold, string message)
    {
        if (!condition) return;
        if (existingAlerts.Any(a => a.SiloId == reading.SiloId
            && a.ThermopendantId == reading.ThermopendantId
            && a.PointIndex == reading.PointIndex
            && a.AlertType == type
            && a.IsActive)) return;

        result.Add(new AlertEvent
        {
            SiloId = reading.SiloId,
            ThermopendantId = reading.ThermopendantId,
            AlertType = type,
            PointIndex = reading.PointIndex,
            Value = reading.Temperature,
            Threshold = threshold,
            Message = message,
            Timestamp = DateTime.UtcNow,
            IsActive = true
        });
    }
}
