namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseMediaFileReadModel(
    int MediaFileId,
    string RelativePath,
    long SizeBytes,
    string? ContainerFormat,
    TimeSpan? Duration,
    ReleaseVideoStreamReadModel? VideoStream,
    IReadOnlyList<ReleaseAudioStreamReadModel> AudioStreams,
    IReadOnlyList<ReleaseSubtitleStreamReadModel> SubtitleStreams,
    string MediaInfoText
);

public record ReleaseVideoStreamReadModel(
    int StreamIndex,
    string Codec,
    string? CodecProfile,
    bool IsDefault,
    string? Language,
    string? Title,
    int? Width,
    int? Height,
    double? Fps,
    string? PixelFormat,
    int? BitrateKbps
);

public record ReleaseAudioStreamReadModel(
    int StreamIndex,
    string Codec,
    string? CodecProfile,
    bool IsDefault,
    string? Language,
    string? Title,
    int? SampleRate,
    string? ChannelLayout,
    int? Channels,
    int? BitrateKbps
);

public record ReleaseSubtitleStreamReadModel(
    int StreamIndex,
    string Codec,
    bool IsDefault,
    bool Forced,
    string? Language,
    string? Title
);
