using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public class ReleaseTemplateFormModel
{
    public string Name { get; set; } = string.Empty;

    public ReleaseType ReleaseType { get; set; } = ReleaseType.Managed;

    public int ReleaseGroupId { get; set; }

    public bool IsEdit { get; set; }

    public int? ReleaseTemplateId { get; set; }
}
