using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Models;

namespace Termometriya.Server.Services;

public class ElevatorConfig
{
    public List<CultureConfig> Cultures { get; set; } = [];
    public List<LineConfig> Lines { get; set; } = [];
}

public class CultureConfig
{
    public string Name { get; set; } = string.Empty;
    public double NormTemp { get; set; } = 25;
    public double WarnTemp { get; set; } = 30;
    public double CriticalTemp { get; set; } = 35;
    public double GradientWarn { get; set; } = 1.0;
    public double GradientCritical { get; set; } = 2.0;
    public double DeviationThreshold { get; set; } = 3.0;
    public double HighTempThreshold { get; set; } = 30;
    public double HighTempGradient { get; set; } = 5.0;
    public double CriticalHighTemp50 { get; set; } = 50;
}

public class LineConfig
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<SiloConfig> Silos { get; set; } = [];
}

public class SiloConfig
{
    public int Number { get; set; }
    public double FillLevel { get; set; }
    public double Capacity { get; set; } = 1000;
    public string CultureName { get; set; } = string.Empty;
    public List<PendantConfig> Pendants { get; set; } = [];
}

public class PendantConfig
{
    public int PositionIndex { get; set; }
    public int PointCount { get; set; } = 30;
    public bool IsCentral { get; set; }
}

public class ElevatorConfigService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _configPath;
    private readonly ILogger<ElevatorConfigService> _logger;

    public ElevatorConfigService(IServiceScopeFactory scopeFactory, ILogger<ElevatorConfigService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "elevator-config.jsonc");
        if (!File.Exists(_configPath))
            _configPath = Path.Combine(Directory.GetCurrentDirectory(), "elevator-config.jsonc");
    }

    public async Task<ElevatorConfig> LoadAsync()
    {
        if (!File.Exists(_configPath))
            return new ElevatorConfig();

        var json = await File.ReadAllTextAsync(_configPath);
        var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
        return JsonSerializer.Deserialize<ElevatorConfig>(json, options) ?? new();
    }

    public async Task SaveAsync(ElevatorConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(_configPath, json);
    }

    public async Task SyncToDbAsync(ElevatorConfig config)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Cultures
        foreach (var cc in config.Cultures)
        {
            var existing = await db.Cultures.FirstOrDefaultAsync(c => c.Name == cc.Name);
            if (existing != null)
            {
                existing.NormTemp = cc.NormTemp;
                existing.WarnTemp = cc.WarnTemp;
                existing.CriticalTemp = cc.CriticalTemp;
                existing.GradientWarn = cc.GradientWarn;
                existing.GradientCritical = cc.GradientCritical;
                existing.DeviationThreshold = cc.DeviationThreshold;
                existing.HighTempThreshold = cc.HighTempThreshold;
                existing.HighTempGradient = cc.HighTempGradient;
                existing.CriticalHighTemp50 = cc.CriticalHighTemp50;
            }
            else
            {
                db.Cultures.Add(new Culture
                {
                    Name = cc.Name,
                    NormTemp = cc.NormTemp,
                    WarnTemp = cc.WarnTemp,
                    CriticalTemp = cc.CriticalTemp,
                    GradientWarn = cc.GradientWarn,
                    GradientCritical = cc.GradientCritical,
                    DeviationThreshold = cc.DeviationThreshold,
                    HighTempThreshold = cc.HighTempThreshold,
                    HighTempGradient = cc.HighTempGradient,
                    CriticalHighTemp50 = cc.CriticalHighTemp50
                });
            }
        }
        await db.SaveChangesAsync();

        // Lines + Silos + Pendants
        var displayOrder = 0;
        foreach (var lc in config.Lines)
        {
            displayOrder++;
            var line = await db.ElevatorLines.FirstOrDefaultAsync(l => l.DisplayOrder == lc.DisplayOrder);
            if (line != null)
                line.Name = lc.Name;
            else
            {
                line = new ElevatorLine { Name = lc.Name, DisplayOrder = lc.DisplayOrder };
                db.ElevatorLines.Add(line);
                await db.SaveChangesAsync();
            }

            foreach (var sc in lc.Silos)
            {
                var silo = await db.Silos.FirstOrDefaultAsync(s => s.LineId == line.Id && s.Number == sc.Number);
                var culture = await db.Cultures.FirstOrDefaultAsync(c => c.Name == sc.CultureName);

                if (silo != null)
                {
                    silo.FillLevel = sc.FillLevel;
                    silo.Capacity = sc.Capacity;
                    if (culture != null) silo.CultureId = culture.Id;
                }
                else
                {
                    silo = new Silo
                    {
                        LineId = line.Id,
                        Number = sc.Number,
                        FillLevel = sc.FillLevel,
                        Capacity = sc.Capacity,
                        CultureId = culture?.Id ?? 1
                    };
                    db.Silos.Add(silo);
                    await db.SaveChangesAsync();
                }

                // Sync pendants
                var existingPendants = await db.Thermopendants
                    .Where(t => t.SiloId == silo.Id)
                    .ToListAsync();

                foreach (var existingP in existingPendants)
                    existingP.IsActive = false;

                for (int i = 0; i < sc.Pendants.Count; i++)
                {
                    var pc = sc.Pendants[i];
                    var match = existingPendants.FirstOrDefault(t => t.PositionIndex == pc.PositionIndex);
                    if (match != null)
                    {
                        match.IsActive = true;
                        match.PointCount = pc.PointCount;
                        match.DisplayOrder = i;
                        match.IsCentral = pc.IsCentral;
                    }
                    else
                    {
                        db.Thermopendants.Add(new Thermopendant
                        {
                            SiloId = silo.Id,
                            PositionIndex = pc.PositionIndex,
                            PointCount = pc.PointCount,
                            DisplayOrder = i,
                            IsActive = true,
                            IsCentral = pc.IsCentral
                        });
                    }
                }
            }
        }
        await db.SaveChangesAsync();
    }

    public async Task<ElevatorConfig> LoadFromDbAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lines = await db.ElevatorLines
            .Include(l => l.Silos).ThenInclude(s => s.Culture)
            .Include(l => l.Silos).ThenInclude(s => s.Thermopendants)
            .OrderBy(l => l.DisplayOrder)
            .ToListAsync();

        var cultures = await db.Cultures.ToListAsync();

        return new ElevatorConfig
        {
            Cultures = cultures.Select(c => new CultureConfig
            {
                Name = c.Name,
                NormTemp = c.NormTemp,
                WarnTemp = c.WarnTemp,
                CriticalTemp = c.CriticalTemp,
                GradientWarn = c.GradientWarn,
                GradientCritical = c.GradientCritical,
                DeviationThreshold = c.DeviationThreshold,
                HighTempThreshold = c.HighTempThreshold,
                HighTempGradient = c.HighTempGradient,
                CriticalHighTemp50 = c.CriticalHighTemp50
            }).ToList(),
            Lines = lines.Select(l => new LineConfig
            {
                Name = l.Name,
                DisplayOrder = l.DisplayOrder,
                Silos = l.Silos.OrderBy(s => s.Number).Select(s => new SiloConfig
                {
                    Number = s.Number,
                    FillLevel = s.FillLevel,
                    Capacity = s.Capacity,
                    CultureName = s.Culture?.Name ?? "",
                    Pendants = s.Thermopendants.Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).Select(t => new PendantConfig
                    {
                        PositionIndex = t.PositionIndex,
                        PointCount = t.PointCount,
                        IsCentral = t.IsCentral
                    }).ToList()
                }).ToList()
            }).ToList()
        };
    }

    public async Task InitFromFileIfNeededAsync()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("No elevator-config.jsonc found, skipping file init");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.ElevatorLines.AnyAsync())
        {
            _logger.LogInformation("DB already has data, skipping file init");
            return;
        }

        _logger.LogInformation("Loading config from elevator-config.jsonc...");
        var config = await LoadAsync();
        await SyncToDbAsync(config);
        _logger.LogInformation("Config synced to DB from elevator-config.jsonc");
    }
}
