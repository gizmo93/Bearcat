namespace Bearcat.Domain.Entities;

public class ArchiveStorageFolder
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Path { get; set; }

    public bool IsActive { get; set; }

    public int MinimumFreeSpaceGb { get; set; }

    public int Priority { get; set; }

    public bool UseLocalWorkingCopyForReuploads { get; set; }
}
