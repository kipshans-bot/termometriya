using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Services;
using Termometriya.Server.Services.DqtService;

namespace Termometriya.Server.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ElevatorConfigService _elevatorConfig;
    private readonly ModbusTcpSimulator _simulator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext db, ElevatorConfigService elevatorConfig,
        ModbusTcpSimulator simulator, ILogger<AdminController> logger)
    {
        _db = db;
        _elevatorConfig = elevatorConfig;
        _simulator = simulator;
        _logger = logger;
    }

    private static string GetLaunchPath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path)) path = "dotnet";
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            path = "dotnet";
        return path;
    }

    private static string GetLaunchArgs()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length <= 1) return "";
        var skip = args[0].EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        return string.Join(" ", args.Skip(1 + skip).Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
    }

    private void RestartServer()
    {
        _logger.LogInformation("Initiating server restart...");
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = GetLaunchPath(),
                    Arguments = GetLaunchArgs(),
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart server");
            }
            Environment.Exit(0);
        });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var siloCount = await _db.Silos.CountAsync();
        var pendantCount = await _db.Thermopendants.CountAsync(t => t.IsActive);
        var alertCount = await _db.AlertEvents.CountAsync(a => a.IsActive);
        var configExists = System.IO.File.Exists(_elevatorConfig.ResolvedPath);

        return Ok(new
        {
            SiloCount = siloCount,
            PendantCount = pendantCount,
            ActiveAlertCount = alertCount,
            ConfigExists = configExists,
            Uptime = (DateTime.UtcNow - _startTime).TotalSeconds,
            Port = 5000,
            AdminPort = 5001
        });
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var path = _elevatorConfig.ResolvedPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return NotFound("Config file not found");

        var content = await System.IO.File.ReadAllTextAsync(path);
        return Ok(new { path, content });
    }

    [HttpPost("config")]
    public async Task<IActionResult> SaveConfig([FromBody] AdminConfigDto dto)
    {
        var path = _elevatorConfig.ResolvedPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return NotFound("Config file not found");

        await System.IO.File.WriteAllTextAsync(path, dto.Content);
        _logger.LogInformation("Config saved, re-syncing to DB...");

        var config = await _elevatorConfig.LoadAsync();
        await _elevatorConfig.SyncToDbAsync(config, _db);
        _simulator.ResetAll();

        RestartServer();
        return Ok(new { status = "saved", restarting = true });
    }

    [HttpPost("clear-db")]
    public async Task<IActionResult> ClearDb()
    {
        _logger.LogWarning("Clearing database...");
        _db.AlertEvents.RemoveRange(_db.AlertEvents);
        _db.SensorReadings.RemoveRange(_db.SensorReadings);
        _db.Thermopendants.RemoveRange(_db.Thermopendants);
        _db.Silos.RemoveRange(_db.Silos);
        await _db.SaveChangesAsync();

        var config = await _elevatorConfig.LoadAsync();
        if (config.Cultures.Count > 0 || config.Lines.Count > 0)
            await _elevatorConfig.SyncToDbAsync(config, _db);
        else
            await SeedData.Initialize(_db);

        _logger.LogWarning("Database cleared and re-seeded");
        return Ok(new { status = "cleared" });
    }

    [HttpPost("restart-server")]
    public IActionResult RestartServerApi()
    {
        RestartServer();
        return Ok(new { status = "restarting" });
    }

    [HttpPost("restart-polling")]
    public IActionResult RestartPolling()
    {
        _simulator.ResetAll();
        _logger.LogInformation("Polling restarted");
        return Ok(new { status = "restarted" });
    }

    private static readonly DateTime _startTime = DateTime.UtcNow;
}

public class AdminConfigDto
{
    public string Content { get; set; } = "";
}
