namespace Bearcat.Domain.Entities;

public class ReleaseNfo
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public int? ReleaseInfoId { get; set; }

    public string FileName { get; set; } = null!;

    public string Content { get; set; } = null!;
}
