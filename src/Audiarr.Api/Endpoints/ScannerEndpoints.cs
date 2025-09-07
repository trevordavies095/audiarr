using Microsoft.AspNetCore.Authorization;
using Audiarr.Services.Auth;
using Audiarr.Services.Library;
using Audiarr.Services.Background;
using Audiarr.Core.Interfaces;

namespace Audiarr.Api.Endpoints;

public static class ScannerEndpoints
{
    public static void MapScannerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/scanner")
            .RequireAuthorization()
            .WithTags("Scanner");

        group.MapPost("/scan", [Authorize] async (
            ScanRequest request,
            ScannerBackgroundService scannerService) =>
        {
            var queued = await scannerService.QueueScanAsync(request);
            if (queued)
            {
                return Results.Accepted($"/api/v2/scanner/status/{request.RequestId}", new
                {
                    message = "Scan request queued",
                    requestId = request.RequestId,
                    path = request.LibraryPath
                });
            }

            return Results.Problem("Failed to queue scan request", statusCode: 500);
        })
        .WithName("QueueLibraryScan")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Queue a library scan",
            Description = "Queues a scan of the specified library path to find and index audio files"
        });

        group.MapPost("/scan/single", [Authorize] async (
            string filePath,
            ILibraryScanner scanner,
            CancellationToken cancellationToken) =>
        {
            if (!System.IO.File.Exists(filePath))
            {
                return Results.NotFound(new { error = "File not found" });
            }

            if (!scanner.IsAudioFile(filePath))
            {
                return Results.BadRequest(new { error = "Not an audio file" });
            }

            var result = await scanner.ScanFileAsync(filePath, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ScanSingleFile")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Scan a single file",
            Description = "Scans a single audio file and adds/updates it in the library"
        });

        group.MapGet("/supported-formats", () =>
        {
            var formats = new[]
            {
                ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", 
                ".wav", ".wma", ".alac", ".ape", ".wv", ".mka"
            };
            return Results.Ok(new { formats });
        })
        .WithName("GetSupportedFormats")
        .WithOpenApi(operation => new(operation)
        {
            Summary = "Get supported audio formats",
            Description = "Returns a list of audio file extensions supported by the scanner"
        });
    }
}