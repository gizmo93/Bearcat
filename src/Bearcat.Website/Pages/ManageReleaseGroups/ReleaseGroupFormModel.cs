namespace Bearcat.Website.Pages.ManageReleaseGroups;

public class ReleaseGroupFormModel
{
    public string Name { get; set; } = string.Empty;

    public bool EnableAutomaticReuploads { get; set; }

    public int NumberOfHoursUntilReupload { get; set; }

    public int? QualityProfileId { get; set; }

    public bool IsEdit { get; set; }

    public int? ReleaseGroupId { get; set; }
}
