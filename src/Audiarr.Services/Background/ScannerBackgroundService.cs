using System.Threading.Channels;
using Audiarr.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Audiarr.Services.Background;

public class ScannerBackgroundService : BackgroundService
{
    private readonly Channel<ScanRequest> _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScannerBackgroundService> _logger;
    // private readonly IHubContext<ScanHub> _hubContext; // TODO: Move to API project

    public ScannerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ScannerBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        // _hubContext = hubContext; // TODO: Move to API project
        
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
                    
                    // TODO: Send progress to SignalR clients from API project
                });

                var result = await scanner.ScanAsync(request.LibraryPath, progress, stoppingToken);
                
                _logger.LogInformation("Scan completed: {RequestId}. Duration: {Duration}, New: {New}, Updated: {Updated}, Errors: {Errors}",
                    request.RequestId, result.Duration, result.NewTracks, result.UpdatedTracks, result.Errors);
                
                // TODO: Send completion to SignalR clients from API project
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