using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Data;
using Termometriya.Server.Hubs;
using Termometriya.Server.Services;
using Termometriya.Server.Services.DqtService;

var baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
    ?? Environment.CurrentDirectory
    ?? AppContext.BaseDirectory;
Console.Error.WriteLine($"DEBUG baseDir={baseDir} cwd={Environment.CurrentDirectory} asmLoc={System.Reflection.Assembly.GetExecutingAssembly().Location}");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = baseDir,
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var dbPath = builder.Configuration.GetConnectionString("Default") ?? $"Data Source={Path.Combine(baseDir, "termometriya.db")}";
    options.UseSqlite(dbPath);
});

builder.Services.AddSingleton<ModbusTcpSimulator>();
builder.Services.AddHostedService<ModbusTcpSimulator>(sp => sp.GetRequiredService<ModbusTcpSimulator>());
builder.Services.AddHostedService<DataPollingService>();

builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<GrainLevelDetector>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton<ElevatorConfigService>();
builder.Services.AddHostedService<TrayService>();

var wwwroot = Path.Combine(baseDir, "wwwroot");
var hasStaticFiles = Directory.Exists(wwwroot) && File.Exists(Path.Combine(wwwroot, "index.html"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var cfgService = scope.ServiceProvider.GetRequiredService<ElevatorConfigService>();
    var configLoaded = await cfgService.InitFromFileIfNeededAsync(db);
    if (!configLoaded)
        await SeedData.Initialize(db);
}

app.UseCors();

if (hasStaticFiles)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");

if (hasStaticFiles)
{
    app.MapFallbackToFile("index.html");
}

app.Run();
