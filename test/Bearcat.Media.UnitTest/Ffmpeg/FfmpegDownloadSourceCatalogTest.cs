using System.Runtime.InteropServices;
using Bearcat.Media.Ffmpeg;
using Shouldly;

namespace Bearcat.Media.UnitTest.Ffmpeg;

public class FfmpegDownloadSourceCatalogTest
{
    private static readonly object[] SupportedPlatforms =
    [
        new object[]
        {
            OSPlatform.Windows,
            Architecture.X64,
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-win64-gpl-9.0.zip",
            FfmpegArchiveFormat.Zip,
            "ffmpeg.exe",
        },
        new object[]
        {
            OSPlatform.Linux,
            Architecture.X64,
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-linux64-gpl-9.0.tar.xz",
            FfmpegArchiveFormat.TarXz,
            "ffmpeg",
        },
        new object[]
        {
            OSPlatform.OSX,
            Architecture.Arm64,
            "https://ffmpeg.martin-riedl.de/redirect/latest/macos/arm64/release/ffmpeg.zip",
            FfmpegArchiveFormat.Zip,
            "ffmpeg",
        },
    ];

    private static readonly object[] UnsupportedPlatforms =
    [
        new object[] { OSPlatform.Linux, Architecture.Arm64 },
        new object[] { OSPlatform.OSX, Architecture.X64 },
        new object[] { OSPlatform.Windows, Architecture.Arm64 },
        new object[] { OSPlatform.FreeBSD, Architecture.X64 },
    ];

    [TestCaseSource(nameof(SupportedPlatforms))]
    public void Find_SupportedPlatform_ReturnsMeasuredSource(
        OSPlatform operatingSystem,
        Architecture architecture,
        string expectedArchiveUrl,
        FfmpegArchiveFormat expectedArchiveFormat,
        string expectedExecutableFileName
    )
    {
        // Act
        var source = FfmpegDownloadSourceCatalog.Find(operatingSystem, architecture);

        // Assert
        source.ShouldNotBeNull();
        source.ArchiveUri.ToString().ShouldBe(expectedArchiveUrl);
        source.ArchiveFormat.ShouldBe(expectedArchiveFormat);
        source.ExecutableFileName.ShouldBe(expectedExecutableFileName);
    }

    [TestCaseSource(nameof(UnsupportedPlatforms))]
    public void Find_UnsupportedPlatform_ReturnsNull(
        OSPlatform operatingSystem,
        Architecture architecture
    )
    {
        // Act
        var source = FfmpegDownloadSourceCatalog.Find(operatingSystem, architecture);

        // Assert
        source.ShouldBeNull();
    }
}
