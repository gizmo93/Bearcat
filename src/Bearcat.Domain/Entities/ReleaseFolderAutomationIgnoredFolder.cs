namespace Bearcat.Domain.Entities;

public class ReleaseFolderAutomationIgnoredFolder
{
    public int Id { get; set; }

    public int ReleaseFolderAutomationId { get; set; }

    public ReleaseFolderAutomation ReleaseFolderAutomation { get; set; } = null!;

    public string FolderPath { get; set; } = null!;

    public DateTime IgnoredAt { get; set; }
}
