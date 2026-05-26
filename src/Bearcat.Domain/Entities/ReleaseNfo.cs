namespace Bearcat.Domain.Entities;

public class ReleaseNfo
{
    public int Id { get; set; }

    public int ReleaseInfoId { get; set; }

    public ReleaseInfo ReleaseInfo { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string Content { get; set; } = null!;
}
