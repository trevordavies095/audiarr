namespace Audiarr.Core.Entities;

public class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = string.Empty;
    public string? TargetUserId { get; set; }
    public string PerformedByUserId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User? TargetUser { get; set; }
    public virtual User PerformedByUser { get; set; } = null!;
}