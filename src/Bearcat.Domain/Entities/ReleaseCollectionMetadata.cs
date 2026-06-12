namespace Bearcat.Domain.Entities;

public class ReleaseCollectionMetadata
{
    public int Id { get; set; }

    public int ReleaseCollectionId { get; set; }

    public ReleaseCollection ReleaseCollection { get; set; } = null!;

    public string SeriesDatabaseClassName { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? CoverUrl { get; set; }

    public string? SeriesDatabaseUrl { get; set; }
}
