using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
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
    RemoteSourceSessionProvider sessionProvider,
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

        var settings = ReadScanSettings();
        var pendingCount = 0;

        foreach (
            var registrationAutomations in automations.GroupBy(automation =>
                automation.RemoteSourceRegistrationId
            )
        )
        {
            pendingCount += await ScanRegistrationAsync(
                RemoteSourceAutomationMatcher.OrderByPriority(registrationAutomations.ToList()),
                settings,
                cancellationToken
            );
        }

        return pendingCount;
    }

    private async Task<int> ScanRegistrationAsync(
        IReadOnlyList<RemoteSourceAutomation> automations,
        ScanSettings settings,
        CancellationToken cancellationToken
    )
    {
        var registration = automations[0].RemoteSourceRegistration;

        var foldersByRemotePath = RemoveFoldersWithUnsafeNames(
            registration,
            await ListFoldersPerRemotePathAsync(registration, automations, cancellationToken)
        );

        var listedFolderPaths = foldersByRemotePath
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

        var foldersByAutomationId = RemoteSourceAutomationMatcher.AssignFoldersToAutomations(
            automations,
            foldersByRemotePath,
            existingDownloadsByPath
        );

        var pendingCount = 0;

        foreach (
            var automation in automations.Where(a => foldersByRemotePath.ContainsKey(a.RemotePath))
        )
        {
            try
            {
                pendingCount += await ScanAutomationAsync(
                    automation: automation,
                    assignedFolders: foldersByAutomationId[automation.Id],
                    existingDownloadsByPath: existingDownloadsByPath,
                    settings: settings,
                    cancellationToken: cancellationToken
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

    private async Task<
        Dictionary<string, IReadOnlyList<RemoteFolderDto>>
    > ListFoldersPerRemotePathAsync(
        RemoteSourceRegistration registration,
        IReadOnlyList<RemoteSourceAutomation> automations,
        CancellationToken cancellationToken
    )
    {
        var foldersByRemotePath = new Dictionary<string, IReadOnlyList<RemoteFolderDto>>(
            StringComparer.Ordinal
        );

        foreach (var pathAutomations in automations.GroupBy(automation => automation.RemotePath))
        {
            try
            {
                foldersByRemotePath[pathAutomations.Key] = await sessionProvider.UseSessionAsync(
                    registration: registration,
                    action: session =>
                        session.ListFoldersAsync(pathAutomations.Key, cancellationToken),
                    cancellationToken: cancellationToken
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

        return foldersByRemotePath;
    }

    private Dictionary<string, IReadOnlyList<RemoteFolderDto>> RemoveFoldersWithUnsafeNames(
        RemoteSourceRegistration registration,
        Dictionary<string, IReadOnlyList<RemoteFolderDto>> foldersByRemotePath
    )
    {
        var safeFoldersByRemotePath = new Dictionary<string, IReadOnlyList<RemoteFolderDto>>(
            StringComparer.Ordinal
        );

        foreach (var (remotePath, folders) in foldersByRemotePath)
        {
            var safeFolders = new List<RemoteFolderDto>();

            foreach (var folder in folders)
            {
                if (RemoteFolderNameValidator.IsSafe(folder.Name))
                {
                    safeFolders.Add(folder);
                    continue;
                }

                logger.LogWarning(
                    "Skipped remote folder {RemoteFolderName} in {RemotePath} on {RemoteSourceName} because its name is not a safe local folder name",
                    folder.Name,
                    remotePath,
                    registration.Name
                );
            }

            safeFoldersByRemotePath[remotePath] = safeFolders;
        }

        return safeFoldersByRemotePath;
    }

    private async Task<int> ScanAutomationAsync(
        RemoteSourceAutomation automation,
        IReadOnlyList<RemoteFolderDto> assignedFolders,
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
            RecordExistingFoldersAsIgnored(
                automation: automation,
                assignedFolders: assignedFolders,
                existingDownloadsByPath: existingDownloadsByPath,
                localNow: localNow
            );
        }
        else
        {
            pendingCount = await RecordNewFoldersAndQueueStableOnesAsync(
                automation: automation,
                assignedFolders: assignedFolders,
                existingDownloadsByPath: existingDownloadsByPath,
                settings: settings,
                localNow: localNow,
                cancellationToken: cancellationToken
            );
        }

        automation.HasCompletedInitialScan = true;

        var assignedFolderPaths = assignedFolders.Select(folder => folder.FullPath).ToHashSet();

        foreach (
            var orphanedDownload in observingDownloads.Where(download =>
                !assignedFolderPaths.Contains(download.RemoteFolderPath)
            )
        )
        {
            repository.Remove(orphanedDownload);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return pendingCount;
    }

    private void RecordExistingFoldersAsIgnored(
        RemoteSourceAutomation automation,
        IReadOnlyList<RemoteFolderDto> assignedFolders,
        IReadOnlyDictionary<string, RemoteSourceDownload> existingDownloadsByPath,
        DateTime localNow
    )
    {
        foreach (
            var folder in assignedFolders.Where(folder =>
                !existingDownloadsByPath.ContainsKey(folder.FullPath)
            )
        )
        {
            repository.Add(
                CreateDownloadRecord(
                    automation: automation,
                    folder: folder,
                    state: RemoteSourceDownloadState.Ignored,
                    fileCountAndSize: new FolderFileCountAndSize(0, 0),
                    localNow: localNow
                )
            );
        }
    }

    private async Task<int> RecordNewFoldersAndQueueStableOnesAsync(
        RemoteSourceAutomation automation,
        IReadOnlyList<RemoteFolderDto> assignedFolders,
        IReadOnlyDictionary<string, RemoteSourceDownload> existingDownloadsByPath,
        ScanSettings settings,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var pendingCount = 0;

        foreach (var folder in assignedFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (existingDownloadsByPath.TryGetValue(folder.FullPath, out var download))
            {
                if (
                    await QueueDownloadIfFolderIsStableAsync(
                        automation: automation,
                        download: download,
                        settings: settings,
                        localNow: localNow,
                        cancellationToken: cancellationToken
                    )
                )
                {
                    pendingCount++;
                }

                continue;
            }

            var fileCountAndSize = await GetFolderFileCountAndSizeAsync(
                automation.RemoteSourceRegistration,
                folder.FullPath,
                cancellationToken
            );
            repository.Add(
                CreateDownloadRecord(
                    automation: automation,
                    folder: folder,
                    state: RemoteSourceDownloadState.Observing,
                    fileCountAndSize: fileCountAndSize,
                    localNow: localNow
                )
            );
        }

        return pendingCount;
    }

    private async Task<bool> QueueDownloadIfFolderIsStableAsync(
        RemoteSourceAutomation automation,
        RemoteSourceDownload download,
        ScanSettings settings,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        CopyAutomationSettingsToDownload(download, automation);

        var fileCountAndSize = await GetFolderFileCountAndSizeAsync(
            automation.RemoteSourceRegistration,
            download.RemoteFolderPath,
            cancellationToken
        );

        var stability = FolderStabilityCheck.GetStability(
            observed: new FolderFileCountAndSize(download.FileCount, download.TotalBytes),
            current: fileCountAndSize,
            lastChangedAt: download.LastChangedAt,
            now: localNow,
            stabilityWindow: settings.StabilityWindow
        );

        if (stability is FolderStability.Changed)
        {
            download.FileCount = fileCountAndSize.FileCount;
            download.TotalBytes = fileCountAndSize.TotalBytes;
            download.LastChangedAt = localNow;

            return false;
        }

        if (
            stability is FolderStability.NotYetStable
            || fileCountAndSize.FileCount == 0
            || fileCountAndSize.TotalBytes < settings.MinimumBytes
        )
        {
            return false;
        }

        download.State = RemoteSourceDownloadState.Pending;

        return true;
    }

    private async Task<FolderFileCountAndSize> GetFolderFileCountAndSizeAsync(
        RemoteSourceRegistration registration,
        string remoteFolderPath,
        CancellationToken cancellationToken
    )
    {
        var files = await sessionProvider.UseSessionAsync(
            registration: registration,
            action: session => session.ListFilesRecursiveAsync(remoteFolderPath, cancellationToken),
            cancellationToken: cancellationToken
        );

        return new FolderFileCountAndSize(files.Count, files.Sum(file => file.SizeBytes));
    }

    private static RemoteSourceDownload CreateDownloadRecord(
        RemoteSourceAutomation automation,
        RemoteFolderDto folder,
        RemoteSourceDownloadState state,
        FolderFileCountAndSize fileCountAndSize,
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
            FileCount = fileCountAndSize.FileCount,
            TotalBytes = fileCountAndSize.TotalBytes,
            LastChangedAt = localNow,
            DiscoveredAt = localNow,
        };

        CopyAutomationSettingsToDownload(download, automation);

        return download;
    }

    private static void CopyAutomationSettingsToDownload(
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

    private ScanSettings ReadScanSettings()
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
