using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning.Repositories;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Stability;
using Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public class RemoteSourceScanService(
    IRemoteSourceScanRepository repository,
    RemoteSourceSessionOpener sessionOpener,
    TimeProvider timeProvider,
    IApplicationConfigurationProvider configuration,
    ILogger<RemoteSourceScanService> logger
)
{
    public async Task<int> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var automations = await repository.GetEnabledAutomationsWithActiveRegistrationAsync(
            cancellationToken
        );

        if (automations.Count == 0)
        {
            return 0;
        }

        var settings = ReadSettings();
        var pendingCount = 0;

        foreach (
            var registrationAutomations in automations.GroupBy(automation =>
                automation.RemoteSourceRegistrationId
            )
        )
        {
            pendingCount += await ProcessRegistrationAsync(
                RemoteSourceAutomationMatcher.OrderByPriority(registrationAutomations.ToList()),
                settings,
                cancellationToken
            );
        }

        return pendingCount;
    }

    private async Task<int> ProcessRegistrationAsync(
        IReadOnlyList<RemoteSourceAutomation> automations,
        ScanSettings settings,
        CancellationToken cancellationToken
    )
    {
        var registration = automations[0].RemoteSourceRegistration;
        IRemoteSourceSession session;

        try
        {
            session = await AcquireSessionAsync(registration, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Remote source {RemoteSourceName} could not be opened, skipping automations {AutomationNames}",
                registration.Name,
                string.Join(", ", automations.Select(automation => automation.Name))
            );

            return 0;
        }

        await using (session)
        {
            var listings = await ListRemotePathsAsync(
                session,
                registration,
                automations,
                cancellationToken
            );
            var listedFolderPaths = listings
                .Values.SelectMany(folders => folders)
                .Select(folder => folder.FullPath)
                .ToList();
            var existingDownloads = await repository.GetDownloadsAsync(
                registration.Id,
                listedFolderPaths,
                cancellationToken
            );
            var existingDownloadsByPath = existingDownloads.ToDictionary(download =>
                download.RemoteFolderPath
            );
            var claims = ResolveClaims(automations, listings, existingDownloadsByPath);

            var pendingCount = 0;

            foreach (var automation in automations.Where(a => listings.ContainsKey(a.RemotePath)))
            {
                try
                {
                    pendingCount += await ProcessAutomationAsync(
                        session,
                        automation,
                        claims[automation.Id],
                        existingDownloadsByPath,
                        settings,
                        cancellationToken
                    );
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    repository.DiscardPendingChanges();

                    logger.LogWarning(
                        exception,
                        "Remote source automation {AutomationName} failed to scan {RemotePath} on {RemoteSourceName}",
                        automation.Name,
                        automation.RemotePath,
                        registration.Name
                    );
                }
            }

            return pendingCount;
        }
    }

    private Task<IRemoteSourceSession> AcquireSessionAsync(
        RemoteSourceRegistration registration,
        CancellationToken cancellationToken
    )
    {
        return sessionOpener.OpenAsync(registration, cancellationToken);
    }

    private async Task<Dictionary<string, IReadOnlyList<RemoteFolderDto>>> ListRemotePathsAsync(
        IRemoteSourceSession session,
        RemoteSourceRegistration registration,
        IReadOnlyList<RemoteSourceAutomation> automations,
        CancellationToken cancellationToken
    )
    {
        var listings = new Dictionary<string, IReadOnlyList<RemoteFolderDto>>(
            StringComparer.Ordinal
        );

        foreach (var pathAutomations in automations.GroupBy(automation => automation.RemotePath))
        {
            try
            {
                listings[pathAutomations.Key] = await session.ListFoldersAsync(
                    pathAutomations.Key,
                    cancellationToken
                );
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "Remote path {RemotePath} on {RemoteSourceName} could not be listed, skipping automations {AutomationNames}",
                    pathAutomations.Key,
                    registration.Name,
                    string.Join(", ", pathAutomations.Select(automation => automation.Name))
                );
            }
        }

        return listings;
    }

    private Dictionary<int, List<RemoteFolderDto>> ResolveClaims(
        IReadOnlyList<RemoteSourceAutomation> automations,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteFolderDto>> listings,
        IReadOnlyDictionary<string, RemoteSourceDownload> existingDownloadsByPath
    )
    {
        var claims = automations.ToDictionary(
            automation => automation.Id,
            _ => new List<RemoteFolderDto>()
        );

        foreach (var (remotePath, folders) in listings)
        {
            var pathAutomations = automations
                .Where(automation => automation.RemotePath == remotePath)
                .ToList();

            foreach (var folder in folders)
            {
                var owner = existingDownloadsByPath.TryGetValue(folder.FullPath, out var download)
                    ? FindExistingOwner(pathAutomations, download, folder)
                    : FindNewOwner(pathAutomations, folder);

                if (owner is not null)
                {
                    claims[owner.Id].Add(folder);
                }
            }
        }

        return claims;
    }

    private static RemoteSourceAutomation? FindExistingOwner(
        IReadOnlyList<RemoteSourceAutomation> pathAutomations,
        RemoteSourceDownload download,
        RemoteFolderDto folder
    )
    {
        if (download.State is not RemoteSourceDownloadState.Observing)
        {
            return null;
        }

        return pathAutomations.FirstOrDefault(automation =>
            automation.Id == download.RemoteSourceAutomationId
            && RemoteSourceAutomationMatcher.Matches(automation, folder.Name)
        );
    }

    private RemoteSourceAutomation? FindNewOwner(
        IReadOnlyList<RemoteSourceAutomation> pathAutomations,
        RemoteFolderDto folder
    )
    {
        var owner = RemoteSourceAutomationMatcher.FindFirstMatch(pathAutomations, folder.Name);

        if (owner is null || RemoteFolderNameValidator.IsSafe(folder.Name))
        {
            return owner;
        }

        logger.LogWarning(
            "Remote source automation {AutomationName} skipped remote folder {RemoteFolderPath} because its name is not a safe local folder name",
            owner.Name,
            folder.FullPath
        );

        return null;
    }

    private async Task<int> ProcessAutomationAsync(
        IRemoteSourceSession session,
        RemoteSourceAutomation automation,
        IReadOnlyList<RemoteFolderDto> claimedFolders,
        IReadOnlyDictionary<string, RemoteSourceDownload> existingDownloadsByPath,
        ScanSettings settings,
        CancellationToken cancellationToken
    )
    {
        var observingDownloads = await repository.GetObservingDownloadsAsync(
            automation.Id,
            cancellationToken
        );
        var localNow = timeProvider.GetLocalNow();
        var pendingCount = 0;

        if (!automation.HasCompletedInitialScan && automation.IgnoreExistingOnFirstScan)
        {
            AddIgnoredDownloads(automation, claimedFolders, existingDownloadsByPath, localNow);
        }
        else
        {
            pendingCount = await ObserveFoldersAsync(
                session,
                automation,
                claimedFolders,
                existingDownloadsByPath,
                settings,
                localNow,
                cancellationToken
            );
        }

        automation.HasCompletedInitialScan = true;

        var claimedPaths = claimedFolders.Select(folder => folder.FullPath).ToHashSet();

        foreach (
            var orphanedDownload in observingDownloads.Where(download =>
                !claimedPaths.Contains(download.RemoteFolderPath)
            )
        )
        {
            repository.Remove(orphanedDownload);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return pendingCount;
    }

    private void AddIgnoredDownloads(
        RemoteSourceAutomation automation,
        IReadOnlyList<RemoteFolderDto> claimedFolders,
        IReadOnlyDictionary<string, RemoteSourceDownload> existingDownloadsByPath,
        DateTime localNow
    )
    {
        foreach (
            var folder in claimedFolders.Where(folder =>
                !existingDownloadsByPath.ContainsKey(folder.FullPath)
            )
        )
        {
            repository.Add(
                CreateDownload(
                    automation,
                    folder,
                    RemoteSourceDownloadState.Ignored,
                    new FolderContentFingerprint(0, 0),
                    localNow
                )
            );
        }
    }

    private async Task<int> ObserveFoldersAsync(
        IRemoteSourceSession session,
        RemoteSourceAutomation automation,
        IReadOnlyList<RemoteFolderDto> claimedFolders,
        IReadOnlyDictionary<string, RemoteSourceDownload> existingDownloadsByPath,
        ScanSettings settings,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var pendingCount = 0;

        foreach (var folder in claimedFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (existingDownloadsByPath.TryGetValue(folder.FullPath, out var download))
            {
                if (
                    await EvaluateObservingDownloadAsync(
                        session,
                        automation,
                        download,
                        settings,
                        localNow,
                        cancellationToken
                    )
                )
                {
                    pendingCount++;
                }

                continue;
            }

            var fingerprint = await GetFingerprintAsync(
                session,
                folder.FullPath,
                cancellationToken
            );
            repository.Add(
                CreateDownload(
                    automation,
                    folder,
                    RemoteSourceDownloadState.Observing,
                    fingerprint,
                    localNow
                )
            );
        }

        return pendingCount;
    }

    private static async Task<bool> EvaluateObservingDownloadAsync(
        IRemoteSourceSession session,
        RemoteSourceAutomation automation,
        RemoteSourceDownload download,
        ScanSettings settings,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        ApplySnapshot(download, automation);

        var fingerprint = await GetFingerprintAsync(
            session,
            download.RemoteFolderPath,
            cancellationToken
        );

        var stability = FolderStabilityGate.Evaluate(
            observed: new FolderContentFingerprint(download.FileCount, download.TotalBytes),
            current: fingerprint,
            lastChangedAt: download.LastChangedAt,
            now: localNow,
            stabilityWindow: settings.StabilityWindow
        );

        if (stability is FolderStability.Changed)
        {
            download.FileCount = fingerprint.FileCount;
            download.TotalBytes = fingerprint.TotalBytes;
            download.LastChangedAt = localNow;

            return false;
        }

        if (
            stability is FolderStability.Settling
            || fingerprint.FileCount == 0
            || fingerprint.TotalBytes < settings.MinimumBytes
        )
        {
            return false;
        }

        download.State = RemoteSourceDownloadState.Pending;

        return true;
    }

    private static async Task<FolderContentFingerprint> GetFingerprintAsync(
        IRemoteSourceSession session,
        string remoteFolderPath,
        CancellationToken cancellationToken
    )
    {
        var files = await session.ListFilesRecursiveAsync(remoteFolderPath, cancellationToken);

        return new FolderContentFingerprint(files.Count, files.Sum(file => file.SizeBytes));
    }

    private static RemoteSourceDownload CreateDownload(
        RemoteSourceAutomation automation,
        RemoteFolderDto folder,
        RemoteSourceDownloadState state,
        FolderContentFingerprint fingerprint,
        DateTime localNow
    )
    {
        var download = new RemoteSourceDownload
        {
            RemoteSourceAutomationId = automation.Id,
            RemoteSourceRegistrationId = automation.RemoteSourceRegistrationId,
            SourceName = automation.RemoteSourceRegistration.Name,
            RemoteFolderPath = folder.FullPath,
            FolderName = folder.Name,
            LocalFolderPath = Path.Combine(automation.TargetPath, folder.Name),
            State = state,
            FileCount = fingerprint.FileCount,
            TotalBytes = fingerprint.TotalBytes,
            LastChangedAt = localNow,
            DiscoveredAt = localNow,
        };

        ApplySnapshot(download, automation);

        return download;
    }

    private static void ApplySnapshot(
        RemoteSourceDownload download,
        RemoteSourceAutomation automation
    )
    {
        download.SourceName = automation.RemoteSourceRegistration.Name;
        download.LocalFolderPath = Path.Combine(automation.TargetPath, download.FolderName);
        download.ReleaseTemplateId = automation.ReleaseTemplateId;
        download.PrimaryLanguageCode = automation.PrimaryLanguageCode;
        download.KeepRawFiles = automation.KeepRawFiles;
    }

    private ScanSettings ReadSettings()
    {
        var stabilityMinutes = configuration.GetValue<RemoteSourceConfiguration>(c =>
            c.StabilityMinutes
        );
        var minimumFolderSizeMegabytes = configuration.GetValue<RemoteSourceConfiguration>(c =>
            c.MinimumFolderSizeMegabytes
        );

        return new ScanSettings(
            StabilityWindow: TimeSpan.FromMinutes(Math.Max(0, stabilityMinutes)),
            MinimumBytes: (long)Math.Max(0, minimumFolderSizeMegabytes) * 1024 * 1024
        );
    }

    private sealed record ScanSettings(TimeSpan StabilityWindow, long MinimumBytes);
}
