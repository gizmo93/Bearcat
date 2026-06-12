using Bearcat.Domain.Shared.ForumPostRendering;

namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

public sealed record ForumPostTemplateRenderModel
{
    public static ForumPostTemplateRenderModel Empty { get; } =
        new()
        {
            Release = ForumPostTemplateReleaseModel.Empty,
            ReleaseInfo = ForumPostTemplateReleaseInfoModel.Empty,
            Uploads = [],
        };

    [ForumPostTemplateVariable("Release data.", IncludeChildren = true)]
    public required ForumPostTemplateReleaseModel Release { get; init; }

    [ForumPostTemplateVariable("First resolved release info.", IncludeChildren = true)]
    public required ForumPostTemplateReleaseInfoModel ReleaseInfo { get; init; }

    [ForumPostTemplateVariable(
        "Loop over upload configurations.",
        LoopVariable = "upload",
        ElementType = typeof(ForumPostTemplateUploadModel)
    )]
    public required IReadOnlyList<ForumPostTemplateUploadModel> Uploads { get; init; }
}

public sealed record ForumPostTemplateReleaseModel
{
    public static ForumPostTemplateReleaseModel Empty { get; } =
        new() { Name = string.Empty, Nfo = string.Empty };

    [ForumPostTemplateVariable("Release name.")]
    public required string Name { get; init; }

    [ForumPostTemplateVariable("Stored NFO content for the release.")]
    public required string Nfo { get; init; }
}

public sealed record ForumPostTemplateReleaseInfoModel
{
    public static ForumPostTemplateReleaseInfoModel Empty { get; } =
        new()
        {
            ReleaseName = string.Empty,
            DatabaseUrl = string.Empty,
            Size = string.Empty,
            SizeNumber = null,
            SizeUnit = string.Empty,
            VideoType = string.Empty,
            AudioType = string.Empty,
            Genre = string.Empty,
            Description = string.Empty,
            Video = ForumPostTemplateMediaInfoModel.Empty,
            Audio = ForumPostTemplateMediaInfoModel.Empty,
            ExternalInfos = [],
        };

    [ForumPostTemplateVariable("Release name from the metadata source.")]
    public required string ReleaseName { get; init; }

    [ForumPostTemplateVariable("Release database URL, e.g. an xrel.to URL.")]
    public required string DatabaseUrl { get; init; }

    [ForumPostTemplateVariable("Size formatted from number and unit.")]
    public required string Size { get; init; }

    [ForumPostTemplateVariable("Size number.")]
    public required int? SizeNumber { get; init; }

    [ForumPostTemplateVariable("Size unit.")]
    public required string SizeUnit { get; init; }

    [ForumPostTemplateVariable("Video type.")]
    public required string VideoType { get; init; }

    [ForumPostTemplateVariable("Audio type.")]
    public required string AudioType { get; init; }

    [ForumPostTemplateVariable("Genre.")]
    public required string Genre { get; init; }

    [ForumPostTemplateVariable("Description or plot.")]
    public required string Description { get; init; }

    [ForumPostTemplateVariable("Video metadata.", IncludeChildren = true)]
    public required ForumPostTemplateMediaInfoModel Video { get; init; }

    [ForumPostTemplateVariable("Audio metadata.", IncludeChildren = true)]
    public required ForumPostTemplateMediaInfoModel Audio { get; init; }

    [ForumPostTemplateVariable(
        "Loop over external metadata entries.",
        LoopVariable = "external_info",
        ElementType = typeof(ForumPostTemplateExternalInfoModel)
    )]
    public required IReadOnlyList<ForumPostTemplateExternalInfoModel> ExternalInfos { get; init; }
}

public sealed record ForumPostTemplateMediaInfoModel
{
    public static ForumPostTemplateMediaInfoModel Empty { get; } =
        new() { Type = string.Empty, Format = string.Empty };

    [ForumPostTemplateVariable("Media type.")]
    public required string Type { get; init; }

    [ForumPostTemplateVariable("Media format.")]
    public required string Format { get; init; }
}

public sealed record ForumPostTemplateExternalInfoModel
{
    [ForumPostTemplateVariable(
        "External info type (Movie, Tv, Game, Console, Software, Xxx, Other)"
    )]
    public required string Type { get; init; }

    [ForumPostTemplateVariable("External info title, e.g. the Name of the Movie.")]
    public required string Title { get; init; }

    [ForumPostTemplateVariable("Loop over URLs for this external info.", LoopVariable = "url")]
    public required IReadOnlyList<string> Urls { get; init; }
}
