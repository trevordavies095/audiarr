using System.Text.Json;
using System.Text.Json.Serialization;

namespace Audiarr.Core.Entities;

public class PlaybackQueue : BaseEntity
{
    public required string UserId { get; set; }

    // JSON serialized queue state
    public string QueueStateJson { get; set; } = "{}";

    // Quick access properties stored as columns for querying
    public int CurrentIndex { get; set; } = 0;
    public string? CurrentTrackId { get; set; }
    public RepeatMode RepeatMode { get; set; } = RepeatMode.None;
    public bool IsShuffled { get; set; } = false;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;

    // Navigation property
    public virtual User User { get; set; } = null!;
    public virtual Track? CurrentTrack { get; set; }

    // Non-mapped property for queue state
    private QueueState? _queueState;

    [JsonIgnore]
    public QueueState QueueState
    {
        get
        {
            if (_queueState == null && !string.IsNullOrEmpty(QueueStateJson))
            {
                _queueState = JsonSerializer.Deserialize<QueueState>(QueueStateJson) ?? new QueueState();
            }
            return _queueState ?? new QueueState();
        }
        set
        {
            _queueState = value;
            QueueStateJson = JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
    }

    // Helper methods
    public void UpdateActivity()
    {
        LastActivity = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool HasTracks()
    {
        return QueueState.TrackIds?.Any() == true;
    }

    public int GetTrackCount()
    {
        return QueueState.TrackIds?.Count ?? 0;
    }

    public void ClearQueue()
    {
        QueueState = new QueueState();
        CurrentIndex = 0;
        CurrentTrackId = null;
        IsShuffled = false;
        UpdateActivity();
    }

    public void SetTracks(List<string> trackIds, bool shuffle = false)
    {
        if (trackIds.Count > 1000)
        {
            throw new ArgumentException("Queue cannot exceed 1000 tracks");
        }

        var state = new QueueState
        {
            TrackIds = trackIds,
            OriginalTrackIds = new List<string>(trackIds)
        };

        if (shuffle)
        {
            state.ShuffledTrackIds = ShuffleList(trackIds);
            IsShuffled = true;
        }

        QueueState = state;
        CurrentIndex = 0;
        CurrentTrackId = trackIds.FirstOrDefault();
        UpdateActivity();
    }

    private List<string> ShuffleList(List<string> list)
    {
        var shuffled = new List<string>(list);
        var random = new Random();

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }
}

public enum RepeatMode
{
    None = 0,
    One = 1,
    All = 2
}

public class QueueState
{
    public List<string>? TrackIds { get; set; }
    public List<string>? OriginalTrackIds { get; set; }
    public List<string>? ShuffledTrackIds { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    public QueueState()
    {
        TrackIds = new List<string>();
        OriginalTrackIds = new List<string>();
    }
}