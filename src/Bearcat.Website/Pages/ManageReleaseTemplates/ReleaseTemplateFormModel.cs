using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public class ReleaseTemplateFormModel
{
    public string Name { get; set; } = string.Empty;

    public ReleaseType ReleaseType { get; set; } = ReleaseType.Managed;

    public int ReleaseGroupId { get; set; }

    public bool UseReleaseCollections { get; set; }

    public ReleaseCollectionDetectionMode ReleaseCollectionDetectionMode { get; set; } =
        ReleaseCollectionDetectionMode.SeriesEpisodePattern;

    public bool IgnoreLanguageInReleaseCollectionName { get; set; } = true;

    public string? ReleaseCollectionPattern { get; set; }

    public string? ReleaseCollectionKeyTemplate { get; set; }

    public string? ReleaseCollectionNameTemplate { get; set; }

    public bool IsEdit { get; set; }

    public int? ReleaseTemplateId { get; set; }
}
