using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Models;

namespace Termometriya.Server.Services;

public class ElevatorConfigService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private string _configPath;
    private readonly ILogger<ElevatorConfigService> _logger;

    public string ResolvedPath => _configPath;

    public ElevatorConfigService(IServiceScopeFactory scopeFactory, ILogger<ElevatorConfigService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var bd = AppContext.BaseDirectory;
        var cwd = Directory.GetCurrentDirectory();
        var asmDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
        _logger.LogInformation("Elevator config search: BaseDir={BaseDir}, CWD={Cwd}, AsmDir={AsmDir}", bd, cwd, asmDir);

        _configPath = Path.Combine(bd, "..", "..", "..", "termometriya-config.jsonc");
        if (!File.Exists(_configPath))
            _configPath = Path.Combine(cwd, "termometriya-config.jsonc");
        if (!File.Exists(_configPath))
            _configPath = Path.Combine(bd, "termometriya-config.jsonc");
        if (!File.Exists(_configPath))
            _configPath = Path.Combine(asmDir, "termometriya-config.jsonc");
        if (!File.Exists(_configPath))
            _configPath = "";

        _logger.LogInformation("Resolved config path: {Path}, exists={Exists}", _configPath, File.Exists(_configPath));
    }

    public async Task<ThermometryConfig> LoadAsync()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogWarning("Config not found at {Path}", _configPath);
            return new ThermometryConfig();
        }

        var json = await File.ReadAllTextAsync(_configPath);
        var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<ThermometryConfig>(json, options) ?? new();
    }

    public async Task SaveAsync(ThermometryConfig config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(_configPath, json);
    }

    public async Task SyncToDbAsync(ThermometryConfig config, AppDbContext db)
    {
        _logger.LogInformation("SyncToDbAsync: {Cultures} cultures, {Lines} lines", config.Cultures.Count, config.Lines.Count);

        db.AlertEvents.RemoveRange(db.AlertEvents);
        db.SensorReadings.RemoveRange(db.SensorReadings);
        db.Thermopendants.RemoveRange(db.Thermopendants);
        db.Silos.RemoveRange(db.Silos);
        await db.SaveChangesAsync();
        _logger.LogInformation("Cleared existing silos/pendants/readings");

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

        // Lines -> Blocks -> Silos -> Sensors -> DB Silos + Pendants
        int displayOrder = 0;
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

            foreach (var block in lc.Blocks)
            {
                foreach (var sc in block.Silos)
                {
                    var silo = await db.Silos.FirstOrDefaultAsync(s => s.LineId == line.Id && s.Number == sc.Number);
                    var culture = await db.Cultures.FirstOrDefaultAsync(c => c.Name == sc.Culture);

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

                    var existingPendants = await db.Thermopendants
                        .Where(t => t.SiloId == silo.Id)
                        .ToListAsync();

                    foreach (var existingP in existingPendants)
                        existingP.IsActive = false;

                    for (int i = 0; i < sc.Sensors.Count; i++)
                    {
                        var sensor = sc.Sensors[i];
                        var match = existingPendants.FirstOrDefault(t => t.PositionIndex == sensor.CableInput);
                        if (match != null)
                        {
                            match.IsActive = true;
                            match.PointCount = sensor.Points;
                            match.DisplayOrder = i;
                            match.IsCentral = sensor.IsCentral;
                        }
                        else
                        {
                            db.Thermopendants.Add(new Thermopendant
                            {
                                SiloId = silo.Id,
                                PositionIndex = sensor.CableInput,
                                PointCount = sensor.Points,
                                DisplayOrder = i,
                                IsActive = true,
                                IsCentral = sensor.IsCentral
                            });
                        }
                    }
                }
            }
        }
        await db.SaveChangesAsync();
    }

    public async Task<ThermometryConfig> LoadFromDbAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lines = await db.ElevatorLines
            .Include(l => l.Silos).ThenInclude(s => s.Culture)
            .Include(l => l.Silos).ThenInclude(s => s.Thermopendants)
            .OrderBy(l => l.DisplayOrder)
            .ToListAsync();

        var cultures = await db.Cultures.ToListAsync();

        return new ThermometryConfig
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
                // DB не хранит блоки и транспорт — возвращаем только silos в блоке (один блок на все)
                Blocks =
                [
                    new BlockConfig
                    {
                        SlaveId = 0,
                        Silos = l.Silos.OrderBy(s => s.Number).Select(s => new SiloConfig
                        {
                            Number = s.Number,
                            FillLevel = s.FillLevel,
                            Capacity = s.Capacity,
                            Culture = s.Culture?.Name ?? "",
                            Sensors = s.Thermopendants.Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).Select(t => new SensorConfig
                            {
                                CableInput = t.PositionIndex,
                                Points = t.PointCount,
                                IsCentral = t.IsCentral
                            }).ToList()
                        }).ToList()
                    }
                ]
            }).ToList()
        };
    }

    public async Task<bool> InitFromFileIfNeededAsync(AppDbContext db)
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("No termometriya-config.jsonc found, skipping file init");
            return false;
        }

        _logger.LogInformation("Loading config from termometriya-config.jsonc...");
        var config = await LoadAsync();
        await SyncToDbAsync(config, db);
        _logger.LogInformation("Config synced to DB from termometriya-config.jsonc");
        return true;
    }
}
