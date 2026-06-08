using Microsoft.AspNetCore.SignalR;
using Termometriya.Server.Hubs;

namespace Termometriya.Server.Services;

public class NotificationService
{
    private readonly IHubContext<MonitoringHub> _hub;

    public NotificationService(IHubContext<MonitoringHub> hub)
    {
        _hub = hub;
    }

    public async Task BroadcastSiloUpdateAsync(object data)
    {
        await _hub.Clients.All.SendAsync("SiloUpdated", data);
    }

    public async Task BroadcastAlertCountsAsync(int critical, int warning, int total, int unacknowledged)
    {
        await _hub.Clients.All.SendAsync("AlertCounts", new { critical, warning, total, unacknowledged });
    }
}
