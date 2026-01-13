namespace Audiarr.Core.Entities;

public class Genre : BaseEntity
{
    public required string Name { get; set; }
    public string? NormalizedName { get; set; }
}
