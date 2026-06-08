namespace Termometriya.Server.Services;

public class TrayService : IHostedService
{
    private readonly string _url;

    public TrayService(IConfiguration config)
    {
        _url = config["Urls"] ?? "http://localhost:5000";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Сервер запущен: {_url}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
