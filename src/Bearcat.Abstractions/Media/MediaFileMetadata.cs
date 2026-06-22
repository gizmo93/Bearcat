namespace Bearcat.Abstractions.Media;

public sealed record MediaFileMetadata(
    string? ContainerFormat,
    long? SizeBytes,
    TimeSpan? Duration,
    MediaVideoStreamMetadata? VideoStream,
    IReadOnlyList<MediaAudioStreamMetadata> AudioStreams,
    IReadOnlyList<MediaSubtitleStreamMetadata> SubtitleStreams
);

public sealed record MediaVideoStreamMetadata(
    int Index,
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

public sealed record MediaAudioStreamMetadata(
    int Index,
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

public sealed record MediaSubtitleStreamMetadata(
    int Index,
    string Codec,
    bool IsDefault,
    bool Forced,
    string? Language,
    string? Title
);
