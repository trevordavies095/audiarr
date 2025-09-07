using System.Threading.Channels;
using Audiarr.Api.Hubs;
using Audiarr.Api.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Audiarr.Api.Services;

public class ScannerBackgroundService : BackgroundService
{
    private readonly Channel<ScanRequest> _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScannerBackgroundService> _logger;
    private readonly IHubContext<ScanHub> _hubContext;

    public ScannerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ScannerBackgroundService> logger,
        IHubContext<ScanHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
        
        // Create unbounded channel for scan requests
        _queue = Channel.CreateUnbounded<ScanRequest>();
    }

    public async Task<bool> QueueScanAsync(ScanRequest request)
    {
        try
        {
            await _queue.Writer.WriteAsync(request);
            _logger.LogInformation("Scan request queued: {RequestId} for path: {Path}", 
                request.RequestId, request.LibraryPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue scan request");
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scanner background service started");

        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing scan request: {RequestId}", request.RequestId);
                
                using var scope = _serviceProvider.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<ILibraryScanner>();
                
                var progress = new Progress<ScanProgress>(async p =>
                {
                    _logger.LogDebug("Scan progress: {Percent:F1}% ({Current}/{Total})", 
                        p.PercentComplete, p.ProcessedFiles, p.TotalFiles);
                    
                    // Send progress to SignalR clients
                    await _hubContext.Clients.All.SendAsync("ScanProgress", new
                    {
                        processed = p.ProcessedFiles,
                        total = p.TotalFiles,
                        message = $"Processing: {p.CurrentFile}",
                        percentComplete = p.PercentComplete
                    });
                });

                var result = await scanner.ScanAsync(request.LibraryPath, progress, stoppingToken);
                
                _logger.LogInformation("Scan completed: {RequestId}. Duration: {Duration}, New: {New}, Updated: {Updated}, Errors: {Errors}",
                    request.RequestId, result.Duration, result.NewTracks, result.UpdatedTracks, result.Errors);
                
                // Send completion to SignalR clients
                await _hubContext.Clients.All.SendAsync("ScanComplete", new
                {
                    totalFiles = result.ProcessedFiles,
                    newTracks = result.NewTracks,
                    updatedTracks = result.UpdatedTracks,
                    errors = result.Errors,
                    completedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scan request: {RequestId}", request.RequestId);
            }
        }

        _logger.LogInformation("Scanner background service stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scanner background service is stopping");
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}