namespace Bearcat.Abstractions.Media;

public interface IFfmpegInstaller
{
    Task<FfmpegStatus> GetStatusAsync(CancellationToken cancellationToken);

    FfmpegInstallState GetInstallState();

    Task<string> InstallLatestAsync(CancellationToken cancellationToken);

    Task<string> EnsureInstalledAsync(CancellationToken cancellationToken);
}
