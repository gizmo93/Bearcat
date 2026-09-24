using System.IO.Enumeration;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders.Repositories;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Stability;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders;

public class LocalFolderScanService(
    ILocalFolderScanRepository repository,
    IFileSystemService fileSystemService,
    ReleaseFromFolderCreator releaseFromFolderCreator,
    TimeProvider timeProvider,
    IApplicationConfigurationProvider configuration,
    INotificationService notificationService
)
{
    private const int MaxConcurrentFolderScans = 4;

    public async Task<int> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var automations = await repository.GetEnabledWithTemplatesAsync(cancellationToken);
        if (automations.Count == 0)
        {
            return 0;
        }

        var candidates = await GetCandidateFoldersAsync(automations, cancellationToken);

        var candidatePaths = candidates
            .Select(candidate => candidate.FolderPath)
            .Distinct()
            .ToList();

        var existingFolderPaths = new HashSet<string>();

        if (candidatePaths.Count > 0)
        {
            existingFolderPaths.UnionWith(
                await repository.GetExistingReleaseFolderPathsAsync(
                    candidatePaths,
                    cancellationToken
                )
            );
            existingFolderPaths.UnionWith(
                await repository.GetExistingArchiveFolderPathsAsync(
                    candidatePaths,
                    cancellationToken
                )
            );
            existingFolderPaths.UnionWith(
                await repository.GetRemoteDownloadFolderPathsAsync(
                    candidatePaths,
                    cancellationToken
                )
            );
        }

        var observations = await repository.GetFolderObservationsAsync(cancellationToken);
        var observationsByPath = observations.ToDictionary(observation => observation.FolderPath);

        var pendingCandidates = candidates
            .Where(candidate => !existingFolderPaths.Contains(candidate.FolderPath))
            .ToList();

        var pendingPaths = pendingCandidates.Select(candidate => candidate.FolderPath).ToHashSet();

        var localNow = timeProvider.GetLocalNow();

        var stabilityWindow = TimeSpan.FromMinutes(
            Math.Max(
                0,
                configuration.GetValue<FolderAutomationConfiguration>(c => c.StabilityMinutes)
            )
        );

        var minimumBytes =
            (long)
                Math.Max(
                    0,
                    configuration.GetValue<FolderAutomationConfiguration>(c =>
                        c.MinimumFolderSizeMegabytes
                    )
                )
            * 1024
            * 1024;

        var processedPaths = new HashSet<string>();
        var createdCount = 0;
        var hasChanges = false;

        foreach (var candidate in pendingCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!processedPaths.Add(candidate.FolderPath))
            {
                continue;
            }

            var fileCountAndSize = fileSystemService.GetFolderFileCountAndSize(
                candidate.FolderPath
            );

            if (!observationsByPath.TryGetValue(candidate.FolderPath, out var observation))
            {
                repository.AddFolderObservation(
                    new ReleaseFolderObservation
                    {
                        FolderPath = candidate.FolderPath,
                        FileCount = fileCountAndSize.FileCount,
                        TotalBytes = fileCountAndSize.TotalBytes,
                        LastChangedAt = localNow,
                    }
                );
                hasChanges = true;
                continue;
            }

            var stability = FolderStabilityCheck.GetStability(
                observed: new FolderFileCountAndSize(observation.FileCount, observation.TotalBytes),
                current: fileCountAndSize,
                lastChangedAt: observation.LastChangedAt,
                now: localNow,
                stabilityWindow: stabilityWindow
            );

            if (stability is FolderStability.Changed)
            {
                observation.FileCount = fileCountAndSize.FileCount;
                observation.TotalBytes = fileCountAndSize.TotalBytes;
                observation.LastChangedAt = localNow;
                hasChanges = true;
                continue;
            }

            if (stability is FolderStability.NotYetStable)
            {
                continue;
            }

            if (fileCountAndSize.TotalBytes < minimumBytes)
            {
                continue;
            }

            await CreateReleaseAsync(candidate, localNow, cancellationToken);

            repository.RemoveFolderObservation(observation);

            createdCount++;
            hasChanges = true;
        }

        var orphanedObservations = observations.Where(observation =>
            !pendingPaths.Contains(observation.FolderPath)
        );

        foreach (var observation in orphanedObservations)
        {
            repository.RemoveFolderObservation(observation);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return createdCount;
    }

    private async Task CreateReleaseAsync(
        ReleaseFolderCandidate candidate,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var release = await releaseFromFolderCreator.CreateAsync(
            releaseTemplate: candidate.Automation.ReleaseTemplate,
            folderPath: candidate.FolderPath,
            primaryLanguageCode: candidate.Automation.PrimaryLanguageCode,
            localNow: localNow,
            cancellationToken: cancellationToken
        );

        repository.Add(release);

        notificationService.Create(
            kind: NotificationKind.ReleaseAutomaticallyCreated,
            message: $"Release '{release.Name}' was created automatically from template '{candidate.Automation.ReleaseTemplate.Name}'",
            entity: release,
            selector: n => n.Release
        );
    }

    private async Task<IReadOnlySet<ReleaseFolderCandidate>> GetCandidateFoldersAsync(
        IReadOnlyList<ReleaseFolderAutomation> automations,
        CancellationToken cancellationToken
    )
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrentFolderScans);

        var scanTasks = automations.Select(async automation =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var folders = await Task.Run(
                    () => fileSystemService.GetFoldersInPath(automation.BasePath),
                    cancellationToken
                );

                return folders
                    .Where(folderPath => MatchesPattern(folderPath, automation.FolderNamePattern))
                    .Select(folderPath => new ReleaseFolderCandidate(automation, folderPath))
                    .ToList();
            }
            finally
            {
                semaphore.Release();
            }
        });

        var candidateGroups = await Task.WhenAll(scanTasks);
        return candidateGroups.SelectMany(group => group).ToHashSet();
    }

    private static bool MatchesPattern(string folderPath, string? folderNamePattern)
    {
        const string magicSynologyFolder = "@eaDir";

        if (folderPath.Contains(magicSynologyFolder, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(folderNamePattern))
        {
            return true;
        }

        return FileSystemName.MatchesSimpleExpression(
            folderNamePattern,
            FolderPathHelper.GetFolderName(folderPath),
            ignoreCase: true
        );
    }

    private sealed record ReleaseFolderCandidate(
        ReleaseFolderAutomation Automation,
        string FolderPath
    );
}
