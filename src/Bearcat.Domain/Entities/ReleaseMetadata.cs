namespace Bearcat.Domain.Entities;

public class ReleaseMetadata
{
    public const string ManualSource = "Manual";

    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public string MetadataDatabaseClassName { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Genre { get; set; }

    public string? Description { get; set; }

    public string? CoverUrl { get; set; }

    public string? MetadataDatabaseUrl { get; set; }
}
