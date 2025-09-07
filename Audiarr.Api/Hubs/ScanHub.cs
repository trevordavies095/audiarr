using Microsoft.AspNetCore.SignalR;

namespace Audiarr.Api.Hubs;

public class ScanHub : Hub
{
    private readonly ILogger<ScanHub> _logger;

    public ScanHub(ILogger<ScanHub> logger)
    {
        _logger = logger;
    }

    public async Task SendProgress(int processed, int total, string message = "")
    {
        await Clients.All.SendAsync("ScanProgress", new
        {
            processed,
            total,
            message,
            percentComplete = total > 0 ? (double)processed / total * 100 : 0
        });
    }

    public async Task SendScanComplete(int totalFiles, int newTracks, int updatedTracks, int errors)
    {
        await Clients.All.SendAsync("ScanComplete", new
        {
            totalFiles,
            newTracks,
            updatedTracks,
            errors,
            completedAt = DateTime.UtcNow
        });
    }

    public async Task SendScanError(string error)
    {
        await Clients.All.SendAsync("ScanError", error);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to ScanHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from ScanHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}