using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Models;

namespace Termometriya.Server.Services.DqtService;

public class DataPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModbusTcpSimulator _simulator;
    private readonly ILogger<DataPollingService> _logger;
    private DateTime _lastGrainCheck = DateTime.MinValue;
    private DateTime _lastCleanup = DateTime.MinValue;
    private static readonly TimeSpan GrainCheckInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);
    private static readonly TimeSpan DataRetention = TimeSpan.FromDays(14);

    public DataPollingService(
        IServiceScopeFactory scopeFactory,
        ModbusTcpSimulator simulator,
        ILogger<DataPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _simulator = simulator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataPollingService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var readings = await _simulator.PollAsync();
                if (readings.Count == 0) continue;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var alertService = scope.ServiceProvider.GetRequiredService<AlertService>();
                    var alertEvents = await alertService.EvaluateAsync(readings);
                    await alertService.ResolveInactiveAlerts(readings);

                    var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                    var siloData = await BuildSiloUpdateDataAsync(readings);
                    await notificationService.BroadcastSiloUpdateAsync(siloData);

                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var counts = new
                    {
                        critical = await db.AlertEvents.CountAsync(a => a.IsActive && a.AlertType == AlertType.Critical, stoppingToken),
                        warning = await db.AlertEvents.CountAsync(a => a.IsActive && (a.AlertType == AlertType.Warning || a.AlertType == AlertType.GradientWarning || a.AlertType == AlertType.DeviationWarning), stoppingToken),
                        total = await db.AlertEvents.CountAsync(a => a.IsActive, stoppingToken),
                        unacknowledged = await db.AlertEvents.CountAsync(a => a.IsActive && a.AcknowledgedAt == null, stoppingToken)
                    };
                    await notificationService.BroadcastAlertCountsAsync(counts.critical, counts.warning, counts.total, counts.unacknowledged);

                    await UpdatePollingModeAsync(readings);

                    var now = DateTime.UtcNow;
                    if (now - _lastGrainCheck >= GrainCheckInterval)
                    {
                        _lastGrainCheck = now;
                        var grainDetector = scope.ServiceProvider.GetRequiredService<GrainLevelDetector>();
                        _ = Task.Run(() => grainDetector.DetectForAllSilosAsync(), stoppingToken);
                    }
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    foreach (var r in readings)
                    {
                        r.Silo = null!;
                        r.Thermopendant = null!;
                    }
                    db.SensorReadings.AddRange(readings);
                    await db.SaveChangesAsync(stoppingToken);

                    var now = DateTime.UtcNow;
                    if (now - _lastCleanup >= CleanupInterval)
                    {
                        _lastCleanup = now;
                        var cutoff = now - DataRetention;
                        var oldReadings = await db.SensorReadings.Where(r => r.Timestamp < cutoff).CountAsync(stoppingToken);
                        if (oldReadings > 0)
                        {
                            db.SensorReadings.RemoveRange(db.SensorReadings.Where(r => r.Timestamp < cutoff));
                            var oldAlerts = await db.AlertEvents.Where(a => !a.IsActive && a.Timestamp < cutoff).CountAsync(stoppingToken);
                            if (oldAlerts > 0)
                                db.AlertEvents.RemoveRange(db.AlertEvents.Where(a => !a.IsActive && a.Timestamp < cutoff));
                            await db.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation("Cleanup: removed {Readings} old readings, {Alerts} old alerts", oldReadings, oldAlerts);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling cycle error");
            }

            var interval = await GetPollingIntervalAsync();
            if (interval > 0)
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }

    private async Task<int> GetPollingIntervalAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = await db.PollingConfigs.FirstOrDefaultAsync();
        if (config == null)
        {
            config = new PollingConfig();
            db.PollingConfigs.Add(config);
            await db.SaveChangesAsync();
        }
        return config.GetCurrentInterval();
    }

    private async Task UpdatePollingModeAsync(List<SensorReading> readings)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = await db.PollingConfigs.FirstOrDefaultAsync();
        if (config == null) return;

        var cultures = await db.Cultures.ToListAsync();
        var hasElevated = false;

        foreach (var group in readings.GroupBy(r => r.SiloId))
        {
            var silo = group.First().Silo;
            var culture = cultures.FirstOrDefault(c => c.Id == silo.CultureId);
            if (culture == null) continue;

            if (group.Any(r => r.IsValid && r.Temperature >= culture.WarnTemp))
            {
                hasElevated = true;
                break;
            }
        }

        var newMode = hasElevated ? PollingMode.Elevated : PollingMode.Normal;
        if (config.CurrentMode != newMode)
        {
            config.CurrentMode = newMode;
            await db.SaveChangesAsync();
            _logger.LogInformation("Polling mode changed to {Mode}", newMode);
        }
    }

    private async Task<object> BuildSiloUpdateDataAsync(List<SensorReading> readings)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendants = await db.Thermopendants.ToListAsync();
        var alerts = await db.AlertEvents.Where(a => a.IsActive).ToListAsync();

        var silos = readings.GroupBy(r => r.SiloId).Select(g =>
        {
            var valid = g.Where(r => r.IsValid).ToList();
            return new
            {
                SiloId = g.Key,
                MaxTemp = valid.Any() ? Math.Round(valid.Max(r => r.Temperature), 1) : 0,
                AvgTemp = valid.Any() ? Math.Round(valid.Average(r => r.Temperature), 1) : 0,
                AvgHumidity = valid.Any() ? Math.Round(valid.Where(r => r.Humidity.HasValue).Average(r => r.Humidity!.Value), 1) : 0,
                PointCount = valid.Count,
                HasActiveAlert = alerts.Any(a => a.SiloId == g.Key),
                AlertLevel = alerts.Where(a => a.SiloId == g.Key).Select(a => a.AlertType).DefaultIfEmpty(AlertType.Normal).Max(),
                Pendants = g.GroupBy(r => r.ThermopendantId).Select(pg =>
                {
                    var pendant = pendants.FirstOrDefault(p => p.Id == pg.Key);
                    var pv = pg.Where(r => r.IsValid).ToList();
                    return new
                    {
                        PendantId = pg.Key,
                        Position = pendant?.PositionIndex ?? 0,
                        MaxTemp = pv.Any() ? Math.Round(pv.Max(r => r.Temperature), 1) : 0,
                        Points = pg.Select(r => new
                        {
                            r.IsValid,
                            r.PointIndex,
                            Temp = r.IsValid ? Math.Round(r.Temperature, 1) : 0,
                            Humidity = r.Humidity.HasValue ? Math.Round(r.Humidity.Value, 1) : (double?)null
                        }).ToList()
                    };
                }).ToList()
            };
        }).ToList();

        return silos;
    }
}
