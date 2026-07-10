using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases;

public class ReleaseFormModel
{
    public string Name { get; set; } = string.Empty;

    public ReleaseType ReleaseType { get; set; } = ReleaseType.Managed;

    public ReleaseContentType ReleaseContentType { get; set; } = ReleaseContentType.Movie;

    public string PrimaryLanguageCode { get; set; } = string.Empty;

    public int ReleaseGroupId { get; set; }

    public string FolderPath { get; set; } = string.Empty;

    public bool IsEdit { get; set; }
}
