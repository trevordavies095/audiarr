namespace Audiarr.Core.Entities;

public class Session : BaseEntity
{
    public required string UserId { get; set; }
    public required string RefreshTokenHash { get; set; }
    public string? DeviceName { get; set; }
    public string? DeviceType { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsExpired && !IsRevoked;
}