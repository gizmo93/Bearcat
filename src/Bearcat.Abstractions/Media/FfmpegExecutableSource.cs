namespace Bearcat.Abstractions.Media;

public enum FfmpegExecutableSource
{
    ConfiguredPath = 1,
    Installed = 2,
    Missing = 3,
    UnsupportedPlatform = 4,
}
