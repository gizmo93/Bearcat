namespace Bearcat.Domain.Entities;

public class ReleaseFolderObservation
{
    public int Id { get; set; }

    public string FolderPath { get; set; } = null!;

    public int FileCount { get; set; }

    public long TotalBytes { get; set; }

    public DateTime LastChangedAt { get; set; }
}
