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
        new()
        {
            Name = string.Empty,
            Nfo = string.Empty,
            MainVideo = ForumPostTemplateMediaFileModel.Empty,
            MediaFiles = [],
        };

    [ForumPostTemplateVariable("Release name.")]
    public required string Name { get; init; }

    [ForumPostTemplateVariable("Stored NFO content for the release.")]
    public required string Nfo { get; init; }

    [ForumPostTemplateVariable("Main video file (largest video file).", IncludeChildren = true)]
    public required ForumPostTemplateMediaFileModel MainVideo { get; init; }

    [ForumPostTemplateVariable(
        "Loop over all extracted media files.",
        LoopVariable = "file",
        ElementType = typeof(ForumPostTemplateMediaFileModel)
    )]
    public required IReadOnlyList<ForumPostTemplateMediaFileModel> MediaFiles { get; init; }
}

public sealed record ForumPostTemplateMediaFileModel
{
    public static ForumPostTemplateMediaFileModel Empty { get; } =
        new()
        {
            Path = string.Empty,
            SizeBytes = 0,
            Duration = string.Empty,
            Container = string.Empty,
            MediaInfo = string.Empty,
            Video = ForumPostTemplateVideoStreamModel.Empty,
            DefaultAudio = ForumPostTemplateAudioStreamModel.Empty,
            AudioStreams = [],
            SubtitleStreams = [],
        };

    [ForumPostTemplateVariable("Relative file path inside the release folder.")]
    public required string Path { get; init; }

    [ForumPostTemplateVariable("Full MediaInfo text dump for the file.")]
    public required string MediaInfo { get; init; }

    [ForumPostTemplateVariable("File size in bytes.")]
    public required long SizeBytes { get; init; }

    [ForumPostTemplateVariable("Duration formatted as hh:mm:ss.")]
    public required string Duration { get; init; }

    [ForumPostTemplateVariable("Container format, e.g. Matroska / WebM.")]
    public required string Container { get; init; }

    [ForumPostTemplateVariable("Video stream metadata.", IncludeChildren = true)]
    public required ForumPostTemplateVideoStreamModel Video { get; init; }

    [ForumPostTemplateVariable("Default (or first) audio stream metadata.", IncludeChildren = true)]
    public required ForumPostTemplateAudioStreamModel DefaultAudio { get; init; }

    [ForumPostTemplateVariable(
        "Loop over audio streams.",
        LoopVariable = "audio",
        ElementType = typeof(ForumPostTemplateAudioStreamModel)
    )]
    public required IReadOnlyList<ForumPostTemplateAudioStreamModel> AudioStreams { get; init; }

    [ForumPostTemplateVariable(
        "Loop over subtitle streams.",
        LoopVariable = "subtitle",
        ElementType = typeof(ForumPostTemplateSubtitleStreamModel)
    )]
    public required IReadOnlyList<ForumPostTemplateSubtitleStreamModel> SubtitleStreams { get; init; }
}

public sealed record ForumPostTemplateVideoStreamModel
{
    public static ForumPostTemplateVideoStreamModel Empty { get; } =
        new()
        {
            Codec = string.Empty,
            Profile = string.Empty,
            Width = null,
            Height = null,
            Resolution = string.Empty,
            Fps = null,
            PixelFormat = string.Empty,
            Language = string.Empty,
            Title = string.Empty,
            BitrateKbps = null,
        };

    [ForumPostTemplateVariable("Video codec, e.g. hevc.")]
    public required string Codec { get; init; }

    [ForumPostTemplateVariable("Video codec profile, e.g. Main 10.")]
    public required string Profile { get; init; }

    [ForumPostTemplateVariable("Frame width in pixels.")]
    public required int? Width { get; init; }

    [ForumPostTemplateVariable("Frame height in pixels.")]
    public required int? Height { get; init; }

    [ForumPostTemplateVariable("Resolution formatted as WxH, e.g. 1920x1080.")]
    public required string Resolution { get; init; }

    [ForumPostTemplateVariable("Frames per second.")]
    public required double? Fps { get; init; }

    [ForumPostTemplateVariable("Pixel format, e.g. yuv420p10le.")]
    public required string PixelFormat { get; init; }

    [ForumPostTemplateVariable("Stream language, e.g. ger.")]
    public required string Language { get; init; }

    [ForumPostTemplateVariable("Stream title.")]
    public required string Title { get; init; }

    [ForumPostTemplateVariable("Bitrate in kbit/s.")]
    public required int? BitrateKbps { get; init; }
}

public sealed record ForumPostTemplateAudioStreamModel
{
    public static ForumPostTemplateAudioStreamModel Empty { get; } =
        new()
        {
            Codec = string.Empty,
            Profile = string.Empty,
            Language = string.Empty,
            Title = string.Empty,
            ChannelLayout = string.Empty,
            Channels = null,
            SampleRate = null,
            BitrateKbps = null,
            IsDefault = false,
        };

    [ForumPostTemplateVariable("Audio codec, e.g. dts.")]
    public required string Codec { get; init; }

    [ForumPostTemplateVariable("Audio codec profile, e.g. DTS-HD MA.")]
    public required string Profile { get; init; }

    [ForumPostTemplateVariable("Stream language, e.g. ger.")]
    public required string Language { get; init; }

    [ForumPostTemplateVariable("Stream title.")]
    public required string Title { get; init; }

    [ForumPostTemplateVariable("Channel layout, e.g. 5.1(side).")]
    public required string ChannelLayout { get; init; }

    [ForumPostTemplateVariable("Channel count.")]
    public required int? Channels { get; init; }

    [ForumPostTemplateVariable("Sample rate in Hz.")]
    public required int? SampleRate { get; init; }

    [ForumPostTemplateVariable("Bitrate in kbit/s.")]
    public required int? BitrateKbps { get; init; }

    [ForumPostTemplateVariable("Whether this is the default stream.")]
    public required bool IsDefault { get; init; }
}

public sealed record ForumPostTemplateSubtitleStreamModel
{
    [ForumPostTemplateVariable("Subtitle codec, e.g. subrip.")]
    public required string Codec { get; init; }

    [ForumPostTemplateVariable("Stream language, e.g. ger.")]
    public required string Language { get; init; }

    [ForumPostTemplateVariable("Stream title.")]
    public required string Title { get; init; }

    [ForumPostTemplateVariable("Whether the subtitle is forced.")]
    public required bool Forced { get; init; }

    [ForumPostTemplateVariable("Whether this is the default stream.")]
    public required bool IsDefault { get; init; }
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
