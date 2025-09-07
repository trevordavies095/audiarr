namespace Audiarr.Core.Entities;

public class User : BaseEntity
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string Role { get; set; } = "user";
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogin { get; set; }
    public string? PreferencesJson { get; set; }
    
    // Navigation properties
    public virtual ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
}