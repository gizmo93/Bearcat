namespace Bearcat.Domain.Entities;

public class ReleaseInfo
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public string NfoDatabaseClassName { get; set; } = null!;

    public string ReleaseName { get; set; } = null!;

    public string? ReleaseDatabaseUrl { get; set; }

    public int? SizeNumber { get; set; }

    public string? SizeUnit { get; set; }

    public string? VideoType { get; set; }

    public string? AudioType { get; set; }

    public string? Genre { get; set; }

    public string? Description { get; set; }

    public string? CoverUrl { get; set; }

    public List<ReleaseExternalInfo> ExternalInfos { get; set; } = [];

    public ReleaseNfo? ReleaseNfo { get; set; }
}
