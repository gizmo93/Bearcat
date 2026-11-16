using System.Runtime.InteropServices;

namespace Bearcat.Media.Ffmpeg;

public static class FfmpegDownloadSourceCatalog
{
    private const string BtbNReleaseUrl =
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest";

    private static readonly FfmpegDownloadSource WindowsX64 = new(
        ArchiveUri: new Uri($"{BtbNReleaseUrl}/ffmpeg-n9.0-latest-win64-gpl-9.0.zip"),
        ArchiveFormat: FfmpegArchiveFormat.Zip,
        ExecutableFileName: "ffmpeg.exe"
    );

    private static readonly FfmpegDownloadSource LinuxX64 = new(
        ArchiveUri: new Uri($"{BtbNReleaseUrl}/ffmpeg-n9.0-latest-linux64-gpl-9.0.tar.xz"),
        ArchiveFormat: FfmpegArchiveFormat.TarXz,
        ExecutableFileName: "ffmpeg"
    );

    private static readonly FfmpegDownloadSource MacOsArm64 = new(
        ArchiveUri: new Uri(
            "https://ffmpeg.martin-riedl.de/redirect/latest/macos/arm64/release/ffmpeg.zip"
        ),
        ArchiveFormat: FfmpegArchiveFormat.Zip,
        ExecutableFileName: "ffmpeg"
    );

    private static readonly List<OSPlatform> SupportedOperatingSystems =
    [
        OSPlatform.Windows,
        OSPlatform.Linux,
        OSPlatform.OSX,
    ];

    public static FfmpegDownloadSource? FindForCurrentPlatform()
    {
        foreach (var operatingSystem in SupportedOperatingSystems)
        {
            if (RuntimeInformation.IsOSPlatform(operatingSystem))
            {
                return Find(operatingSystem, RuntimeInformation.OSArchitecture);
            }
        }

        return null;
    }

    public static FfmpegDownloadSource? Find(OSPlatform operatingSystem, Architecture architecture)
    {
        if (operatingSystem == OSPlatform.Windows && architecture == Architecture.X64)
        {
            return WindowsX64;
        }

        if (operatingSystem == OSPlatform.Linux && architecture == Architecture.X64)
        {
            return LinuxX64;
        }

        if (operatingSystem == OSPlatform.OSX && architecture == Architecture.Arm64)
        {
            return MacOsArm64;
        }

        return null;
    }
}
