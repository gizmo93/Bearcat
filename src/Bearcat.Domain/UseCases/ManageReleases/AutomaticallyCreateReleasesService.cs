using System.IO.Enumeration;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class AutomaticallyCreateReleasesService(
    IAutomaticallyCreateReleasesRepository repository,
    IFileSystemService fileSystemService,
    ReleaseInfoResolutionService releaseInfoResolutionService,
    MediaMetadataService mediaMetadataService,
    TimeProvider timeProvider,
    IArchiverFactory archiverFactory,
    ReleaseCollectionAssignmentService releaseCollectionAssignmentService,
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

            var fingerprint = fileSystemService.GetFolderContentFingerprint(candidate.FolderPath);

            if (!observationsByPath.TryGetValue(candidate.FolderPath, out var observation))
            {
                repository.AddFolderObservation(
                    new ReleaseFolderObservation
                    {
                        FolderPath = candidate.FolderPath,
                        FileCount = fingerprint.FileCount,
                        TotalBytes = fingerprint.TotalBytes,
                        LastChangedAt = localNow,
                    }
                );
                hasChanges = true;
                continue;
            }

            var changed =
                observation.FileCount != fingerprint.FileCount
                || observation.TotalBytes != fingerprint.TotalBytes;

            if (changed)
            {
                observation.FileCount = fingerprint.FileCount;
                observation.TotalBytes = fingerprint.TotalBytes;
                observation.LastChangedAt = localNow;
                hasChanges = true;
                continue;
            }

            if (localNow - observation.LastChangedAt < stabilityWindow)
            {
                continue;
            }

            if (fingerprint.TotalBytes < minimumBytes)
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
        var releaseData = ReleaseService.CreateFromTemplateData(
            releaseTemplate: candidate.Automation.ReleaseTemplate,
            releaseFolderPath: candidate.FolderPath,
            name: null,
            releaseType: candidate.Automation.ReleaseTemplate.ReleaseType,
            archivers: candidate.Automation.ReleaseTemplate.ReleaseType is ReleaseType.Unmanaged
                ? archiverFactory.GetArchivers()
                : [],
            localNow: localNow
        );
        var release = releaseData.Release;

        release.CreatedAt = localNow;

        await releaseCollectionAssignmentService.AssignFromTemplateAsync(
            release: release,
            releaseTemplate: candidate.Automation.ReleaseTemplate,
            uploadConfigMatches: releaseData.UploadConfigMatches,
            cancellationToken: cancellationToken
        );

        await releaseInfoResolutionService.TryResolveAsync(release, cancellationToken);

        await mediaMetadataService.TryExtractAsync(release, cancellationToken);

        repository.Add(release);

        notificationService.CreateInfo(
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
