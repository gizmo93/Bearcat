using Bearcat.Domain.Shared.ForumPostRendering;

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

    [ForumPostTemplateVariable(
        "Loop over upload configurations of the release.",
        LoopVariable = "upload",
        ElementType = typeof(ForumPostTemplateUploadModel)
    )]
    public required IReadOnlyList<ForumPostTemplateUploadModel> Uploads { get; init; }
}
