using System.ComponentModel;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bearcat.Media.Ffmpeg;

public sealed class FfmpegInstaller(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<FfmpegInstaller> logger
) : IFfmpegInstaller, IDisposable
{
    public const string HttpClientName = "ffmpeg-download";

    private const string ConfiguredPathKey = "Media:FfmpegPath";

    private readonly SemaphoreSlim installLock = new(1, 1);

    private readonly FfmpegDownloadSource? downloadSource =
        FfmpegDownloadSourceCatalog.FindForCurrentPlatform();

    private readonly string installDirectory = Path.Combine(
        BearcatDataDirectory.Resolve(configuration),
        "tools",
        "ffmpeg"
    );

    private volatile bool isInstallRunning;
    private volatile string? lastErrorMessage;

    private string InstalledExecutablePath =>
        Path.Combine(installDirectory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");

    public async Task<FfmpegStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var configuredPath = GetConfiguredPath();

        if (configuredPath is not null)
        {
            return new FfmpegStatus(
                Source: FfmpegExecutableSource.ConfiguredPath,
                ExecutablePath: configuredPath,
                VersionLine: await ReadVersionLineAsync(configuredPath, cancellationToken),
                InstalledAt: null
            );
        }

        if (File.Exists(InstalledExecutablePath))
        {
            return new FfmpegStatus(
                Source: FfmpegExecutableSource.Installed,
                ExecutablePath: InstalledExecutablePath,
                VersionLine: await ReadVersionLineAsync(InstalledExecutablePath, cancellationToken),
                InstalledAt: File.GetLastWriteTimeUtc(InstalledExecutablePath)
            );
        }

        return new FfmpegStatus(
            Source: downloadSource is null
                ? FfmpegExecutableSource.UnsupportedPlatform
                : FfmpegExecutableSource.Missing,
            ExecutablePath: null,
            VersionLine: null,
            InstalledAt: null
        );
    }

    public FfmpegInstallState GetInstallState()
    {
        return new FfmpegInstallState(
            IsRunning: isInstallRunning,
            LastErrorMessage: lastErrorMessage
        );
    }

    public async Task<string> InstallLatestAsync(CancellationToken cancellationToken)
    {
        await installLock.WaitAsync(cancellationToken);

        try
        {
            return await InstallWhileLockedAsync(cancellationToken);
        }
        finally
        {
            installLock.Release();
        }
    }

    public async Task<string> EnsureInstalledAsync(CancellationToken cancellationToken)
    {
        var configuredPath = GetConfiguredPath();

        if (configuredPath is not null)
        {
            return configuredPath;
        }

        if (File.Exists(InstalledExecutablePath))
        {
            return InstalledExecutablePath;
        }

        await installLock.WaitAsync(cancellationToken);

        try
        {
            return File.Exists(InstalledExecutablePath)
                ? InstalledExecutablePath
                : await InstallWhileLockedAsync(cancellationToken);
        }
        finally
        {
            installLock.Release();
        }
    }

    public void Dispose()
    {
        installLock.Dispose();
    }

    private string? GetConfiguredPath()
    {
        var configuredPath = configuration[ConfiguredPathKey];
        return string.IsNullOrWhiteSpace(configuredPath) ? null : configuredPath;
    }

    private async Task<string> InstallWhileLockedAsync(CancellationToken cancellationToken)
    {
        if (downloadSource is null)
        {
            throw new InvalidOperationException(
                $"No ffmpeg download is available for this platform. Configure {ConfiguredPathKey}."
            );
        }

        isInstallRunning = true;
        lastErrorMessage = null;

        var temporaryDirectory = Path.Combine(
            Path.GetDirectoryName(installDirectory)!,
            $"ffmpeg-download-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            return await DownloadAndInstallAsync(
                source: downloadSource,
                temporaryDirectory: temporaryDirectory,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
        {
            lastErrorMessage = exception.Message;
            logger.LogError(exception, "Failed to install ffmpeg");
            throw;
        }
        finally
        {
            isInstallRunning = false;
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private async Task<string> DownloadAndInstallAsync(
        FfmpegDownloadSource source,
        string temporaryDirectory,
        CancellationToken cancellationToken
    )
    {
        var archiveFilePath = Path.Combine(temporaryDirectory, source.ArchiveUri.Segments.Last());

        using (var httpClient = httpClientFactory.CreateClient(HttpClientName))
        {
            await using var archiveStream = await httpClient.GetStreamAsync(
                source.ArchiveUri,
                cancellationToken
            );

            await using var fileStream = File.Create(archiveFilePath);
            await archiveStream.CopyToAsync(fileStream, cancellationToken);
        }

        var extractedExecutablePath = Path.Combine(temporaryDirectory, source.ExecutableFileName);

        FfmpegArchiveExtractor.ExtractExecutable(
            archiveFilePath: archiveFilePath,
            archiveFormat: source.ArchiveFormat,
            executableFileName: source.ExecutableFileName,
            destinationFilePath: extractedExecutablePath
        );

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                extractedExecutablePath,
                UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute
            );
        }

        await FfmpegProcessRunner.RunAsync(
            executablePath: extractedExecutablePath,
            arguments: ["-version"],
            workingDirectory: null,
            cancellationToken: cancellationToken
        );

        File.SetLastWriteTimeUtc(extractedExecutablePath, DateTime.UtcNow);
        Directory.CreateDirectory(installDirectory);
        File.Move(extractedExecutablePath, InstalledExecutablePath, overwrite: true);

        logger.LogInformation(
            "Installed ffmpeg from {SourceUrl} to {ExecutablePath}",
            source.ArchiveUri,
            InstalledExecutablePath
        );

        return InstalledExecutablePath;
    }

    private async Task<string?> ReadVersionLineAsync(
        string executablePath,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var output = await FfmpegProcessRunner.RunAsync(
                executablePath: executablePath,
                arguments: ["-version"],
                workingDirectory: null,
                cancellationToken: cancellationToken
            );

            var versionLine = output.Split('\n', StringSplitOptions.TrimEntries).First();
            var copyrightIndex = versionLine.IndexOf(" Copyright", StringComparison.Ordinal);
            return copyrightIndex > 0 ? versionLine[..copyrightIndex] : versionLine;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(
                exception,
                "Failed to read the ffmpeg version from {ExecutablePath}",
                executablePath
            );
            return null;
        }
    }
}
