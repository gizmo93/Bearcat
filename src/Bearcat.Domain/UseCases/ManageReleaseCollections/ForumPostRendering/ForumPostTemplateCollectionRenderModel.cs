using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

public sealed record ForumPostTemplateCollectionRenderModel
{
    [ForumPostTemplateVariable("Release collection data.", IncludeChildren = true)]
    public required ForumPostTemplateCollectionModel Collection { get; init; }

    [ForumPostTemplateVariable(
        "Series metadata resolved from a metadata database.",
        IncludeChildren = true
    )]
    public required ForumPostTemplateSeriesModel Series { get; init; }

    [ForumPostTemplateVariable(
        "Loop over the releases in the collection.",
        LoopVariable = "release",
        ElementType = typeof(ForumPostTemplateCollectionReleaseModel)
    )]
    public required IReadOnlyList<ForumPostTemplateCollectionReleaseModel> Releases { get; init; }
}

public sealed record ForumPostTemplateCollectionModel
{
    [ForumPostTemplateVariable("Collection name.")]
    public required string Name { get; init; }

    [ForumPostTemplateVariable("Collection key.")]
    public required string Key { get; init; }

    [ForumPostTemplateVariable("Release group name.")]
    public required string ReleaseGroup { get; init; }

    [ForumPostTemplateVariable("Primary language name.")]
    public required string PrimaryLanguage { get; init; }
}

public sealed record ForumPostTemplateSeriesModel
{
    public static ForumPostTemplateSeriesModel Empty { get; } =
        new()
        {
            Title = string.Empty,
            Description = string.Empty,
            CoverUrl = string.Empty,
            DatabaseUrl = string.Empty,
        };

    [ForumPostTemplateVariable("Series title.")]
    public required string Title { get; init; }

    [ForumPostTemplateVariable("Series description (German when available).")]
    public required string Description { get; init; }

    [ForumPostTemplateVariable("Series cover image URL.")]
    public required string CoverUrl { get; init; }

    [ForumPostTemplateVariable("Series database URL, e.g. a thetvdb.com URL.")]
    public required string DatabaseUrl { get; init; }
}

public sealed record ForumPostTemplateCollectionReleaseModel
{
    [ForumPostTemplateVariable("Release name.")]
    public required string Name { get; init; }

    [ForumPostTemplateVariable("Primary language name.")]
    public required string PrimaryLanguage { get; init; }

    [ForumPostTemplateVariable("Stored NFO content for the release.")]
    public required string Nfo { get; init; }

    [ForumPostTemplateVariable("Steam app id, e.g. 1172710.")]
    public required string SteamAppId { get; init; }

    [ForumPostTemplateVariable("Steam store page URL.")]
    public required string SteamUrl { get; init; }

    [ForumPostTemplateVariable("IMDb id, e.g. tt0133093.")]
    public required string ImdbId { get; init; }

    [ForumPostTemplateVariable("IMDb title URL.")]
    public required string ImdbUrl { get; init; }

    [ForumPostTemplateVariable("Main video file (largest video file).", IncludeChildren = true)]
    public required ForumPostTemplateMediaFileModel MainVideo { get; init; }

    [ForumPostTemplateVariable(
        "Loop over all extracted media files.",
        LoopVariable = "file",
        ElementType = typeof(ForumPostTemplateMediaFileModel)
    )]
    public required IReadOnlyList<ForumPostTemplateMediaFileModel> MediaFiles { get; init; }

    [ForumPostTemplateVariable("First resolved release info.", IncludeChildren = true)]
    public required ForumPostTemplateReleaseInfoModel ReleaseInfo { get; init; }

    [ForumPostTemplateVariable("Parsed release classification.", IncludeChildren = true)]
    public required ForumPostTemplateClassificationModel Classification { get; init; }

    [ForumPostTemplateVariable(
        "Loop over upload configurations.",
        LoopVariable = "upload",
        ElementType = typeof(ForumPostTemplateUploadModel)
    )]
    public required IReadOnlyList<ForumPostTemplateUploadModel> Uploads { get; init; }

    [ForumPostTemplateVariable(
        "Loop over link crypter containers that contain all hosters of the release as mirrors (container scope \"Release (all hosters as mirrors)\"). One entry per link crypter, so a post can show a single download link.",
        LoopVariable = "crypter",
        ElementType = typeof(ForumPostTemplateLinkCrypterModel)
    )]
    public required IReadOnlyList<ForumPostTemplateLinkCrypterModel> LinkCrypters { get; init; }
}
