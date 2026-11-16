namespace Bearcat.Abstractions.Media;

public sealed record FfmpegStatus(
    FfmpegExecutableSource Source,
    string? ExecutablePath,
    string? VersionLine,
    DateTimeOffset? InstalledAt
);
