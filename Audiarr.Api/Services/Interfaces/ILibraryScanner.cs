using Audiarr.Api.Models.DTOs;

namespace Audiarr.Api.Services.Interfaces;

public interface ILibraryScanner
{
    Task<ScanResult> ScanAsync(string libraryPath, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<ScanResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
    bool IsAudioFile(string filePath);
}

public class ScanResult
{
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int NewTracks { get; set; }
    public int UpdatedTracks { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}

public class ScanProgress
{
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
}

public class ScanRequest
{
    public string LibraryPath { get; set; } = string.Empty;
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}