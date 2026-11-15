namespace Bearcat.Media.Ffmpeg;

public sealed record FfmpegDownloadSource(
    Uri ArchiveUri,
    FfmpegArchiveFormat ArchiveFormat,
    string ExecutableFileName
);
