using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases;

public class ReleaseFormModel
{
    public string Name { get; set; } = string.Empty;

    public ReleaseType? ReleaseType { get; set; }

    public string FolderPath { get; set; } = string.Empty;

    public bool IsEdit { get; set; }
}
