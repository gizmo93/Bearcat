namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

public sealed record ForumPostTemplateRenderModel
{
    public ForumPostTemplateRenderModel(
        ForumPostTemplateReleaseModel release,
        ForumPostTemplateReleaseInfoModel releaseInfo,
        IReadOnlyList<ForumPostTemplateUploadModel> uploads
    )
    {
        Release = release;
        ReleaseInfo = releaseInfo;
        Uploads = uploads;
    }

    public static ForumPostTemplateRenderModel Empty { get; } =
        new(
            release: ForumPostTemplateReleaseModel.Empty,
            releaseInfo: ForumPostTemplateReleaseInfoModel.Empty,
            uploads: []
        );

    [ForumPostTemplateVariable("Release data.", IncludeChildren = true)]
    public ForumPostTemplateReleaseModel Release { get; init; }

    [ForumPostTemplateVariable("First resolved release info.", IncludeChildren = true)]
    public ForumPostTemplateReleaseInfoModel ReleaseInfo { get; init; }

    [ForumPostTemplateVariable(
        "Loop over upload configurations.",
        LoopVariable = "upload",
        ElementType = typeof(ForumPostTemplateUploadModel)
    )]
    public IReadOnlyList<ForumPostTemplateUploadModel> Uploads { get; init; }
}

public sealed record ForumPostTemplateReleaseModel
{
    public ForumPostTemplateReleaseModel(string name, string nfo)
    {
        Name = name;
        Nfo = nfo;
    }

    public static ForumPostTemplateReleaseModel Empty { get; } = new(string.Empty, string.Empty);

    [ForumPostTemplateVariable("Release name.")]
    public string Name { get; init; }

    [ForumPostTemplateVariable("Stored NFO content for the release.")]
    public string Nfo { get; init; }
}

public sealed record ForumPostTemplateReleaseInfoModel
{
    public ForumPostTemplateReleaseInfoModel(
        string releaseName,
        string databaseUrl,
        string size,
        int? sizeNumber,
        string sizeUnit,
        string videoType,
        string audioType,
        string genre,
        string description,
        ForumPostTemplateMediaInfoModel video,
        ForumPostTemplateMediaInfoModel audio,
        IReadOnlyList<ForumPostTemplateExternalInfoModel> externalInfos
    )
    {
        ReleaseName = releaseName;
        DatabaseUrl = databaseUrl;
        Size = size;
        SizeNumber = sizeNumber;
        SizeUnit = sizeUnit;
        VideoType = videoType;
        AudioType = audioType;
        Genre = genre;
        Description = description;
        Video = video;
        Audio = audio;
        ExternalInfos = externalInfos;
    }

    public static ForumPostTemplateReleaseInfoModel Empty { get; } =
        new(
            releaseName: string.Empty,
            databaseUrl: string.Empty,
            size: string.Empty,
            sizeNumber: null,
            sizeUnit: string.Empty,
            videoType: string.Empty,
            audioType: string.Empty,
            genre: string.Empty,
            description: string.Empty,
            video: ForumPostTemplateMediaInfoModel.Empty,
            audio: ForumPostTemplateMediaInfoModel.Empty,
            externalInfos: []
        );

    [ForumPostTemplateVariable("Release name from the metadata source.")]
    public string ReleaseName { get; init; }

    [ForumPostTemplateVariable("Release database URL, e.g. an xrel.to URL.")]
    public string DatabaseUrl { get; init; }

    [ForumPostTemplateVariable("Size formatted from number and unit.")]
    public string Size { get; init; }

    [ForumPostTemplateVariable("Size number.")]
    public int? SizeNumber { get; init; }

    [ForumPostTemplateVariable("Size unit.")]
    public string SizeUnit { get; init; }

    [ForumPostTemplateVariable("Video type.")]
    public string VideoType { get; init; }

    [ForumPostTemplateVariable("Audio type.")]
    public string AudioType { get; init; }

    [ForumPostTemplateVariable("Genre.")]
    public string Genre { get; init; }

    [ForumPostTemplateVariable("Description or plot.")]
    public string Description { get; init; }

    [ForumPostTemplateVariable("Video metadata.", IncludeChildren = true)]
    public ForumPostTemplateMediaInfoModel Video { get; init; }

    [ForumPostTemplateVariable("Audio metadata.", IncludeChildren = true)]
    public ForumPostTemplateMediaInfoModel Audio { get; init; }

    [ForumPostTemplateVariable(
        "Loop over external metadata entries.",
        LoopVariable = "external_info",
        ElementType = typeof(ForumPostTemplateExternalInfoModel)
    )]
    public IReadOnlyList<ForumPostTemplateExternalInfoModel> ExternalInfos { get; init; }
}

public sealed record ForumPostTemplateMediaInfoModel
{
    public ForumPostTemplateMediaInfoModel(string type, string format)
    {
        Type = type;
        Format = format;
    }

    public static ForumPostTemplateMediaInfoModel Empty { get; } =
        new(type: string.Empty, format: string.Empty);

    [ForumPostTemplateVariable("Media type.")]
    public string Type { get; init; }

    [ForumPostTemplateVariable("Media format.")]
    public string Format { get; init; }
}

public sealed record ForumPostTemplateExternalInfoModel
{
    public ForumPostTemplateExternalInfoModel(string type, string title, IReadOnlyList<string> urls)
    {
        Type = type;
        Title = title;
        Urls = urls;
    }

    [ForumPostTemplateVariable(
        "External info type (Movie, Tv, Game, Console, Software, Xxx, Other)"
    )]
    public string Type { get; init; }

    [ForumPostTemplateVariable("External info title, e.g. the Name of the Movie.")]
    public string Title { get; init; }

    [ForumPostTemplateVariable("Loop over URLs for this external info.", LoopVariable = "url")]
    public IReadOnlyList<string> Urls { get; init; }
}

public sealed record ForumPostTemplateUploadModel
{
    public ForumPostTemplateUploadModel(
        string name,
        string hosterName,
        DateTime? uploadedAt,
        string archivePassword,
        IReadOnlyList<string> links,
        IReadOnlyList<ForumPostTemplateLinkCrypterModel> linkCrypters
    )
    {
        Name = name;
        HosterName = hosterName;
        UploadedAt = uploadedAt;
        ArchivePassword = archivePassword;
        Links = links;
        LinkCrypters = linkCrypters;
    }

    [ForumPostTemplateVariable("Upload configuration name.")]
    public string Name { get; init; }

    [ForumPostTemplateVariable("Hoster registration name.")]
    public string HosterName { get; init; }

    [ForumPostTemplateVariable("Latest upload date.")]
    public DateTime? UploadedAt { get; init; }

    [ForumPostTemplateVariable("Archive password of the latest upload.")]
    public string ArchivePassword { get; init; }

    [ForumPostTemplateVariable(
        "Loop over direct hoster links of the latest upload.",
        LoopVariable = "link"
    )]
    public IReadOnlyList<string> Links { get; init; }

    [ForumPostTemplateVariable(
        "Loop over link crypter container links.",
        LoopVariable = "crypter",
        ElementType = typeof(ForumPostTemplateLinkCrypterModel)
    )]
    public IReadOnlyList<ForumPostTemplateLinkCrypterModel> LinkCrypters { get; init; }
}

public sealed record ForumPostTemplateLinkCrypterModel
{
    public ForumPostTemplateLinkCrypterModel(string name, string containerLink, DateTime createdAt)
    {
        Name = name;
        ContainerLink = containerLink;
        CreatedAt = createdAt;
    }

    [ForumPostTemplateVariable("Link crypter registration name.")]
    public string Name { get; init; }

    [ForumPostTemplateVariable("Generated container URL.")]
    public string ContainerLink { get; init; }

    [ForumPostTemplateVariable("Container creation date.")]
    public DateTime CreatedAt { get; init; }
}
