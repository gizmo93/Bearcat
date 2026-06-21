namespace Bearcat.Domain.Entities;

public class PostedLocation
{
    public int Id { get; set; }

    public int? ReleaseId { get; set; }

    public Release? Release { get; set; }

    public int? ReleaseCollectionId { get; set; }

    public ReleaseCollection? ReleaseCollection { get; set; }

    public string Url { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
