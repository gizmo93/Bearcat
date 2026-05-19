namespace Bearcat.Domain.Entities;

public class ReleaseFolderAutomation
{
    public int Id { get; set; }

    public string BasePath { get; set; } = null!;

    public string? FolderNamePattern { get; set; }

    public int ReleaseTemplateId { get; set; }

    public ReleaseTemplate ReleaseTemplate { get; set; } = null!;

    public bool IsEnabled { get; set; }
}
