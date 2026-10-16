using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage.Repositories;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders.Repositories;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders.VerificationAndExtraction;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Stability;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders;

public class LocalFolderScanService(
    ILocalFolderScanRepository repository,
    IReleaseFolderUsageRepository releaseFolderUsageRepository,
    IFileSystemService fileSystemService,
    ReleaseFolderCandidateFinder candidateFinder,
    ReleaseFromFolderCreationService releaseFromFolderCreationService,
    ReleaseFolderVerificationAndExtractionService verificationAndExtractionService,
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

        var settings = ReadScanSettings();
        var candidates = await GetCandidateFoldersAsync(automations, cancellationToken);
        var candidatesWithoutUsedFolder = await GetCandidatesWhoseFolderIsNotUsedYetAsync(
            candidates,
            cancellationToken
        );
        var ignoredFolders = await GetIgnoredFoldersAsync(automations, cancellationToken);

        var localNow = timeProvider.GetLocalNow();

        var hasChanges = IgnoreCandidatesOfAutomationsInInitialScan(
            candidatesWithoutUsedFolder,
            ignoredFolders,
            localNow
        );
        hasChanges |= MarkInitialScanAsCompleted(automations);

        var candidatesToProcess = GetCandidatesWhoseFolderIsNotIgnored(
            candidatesWithoutUsedFolder,
            ignoredFolders
        );

        var observations = await repository.GetFolderObservationsAsync(cancellationToken);
        var observationsByPath = observations.ToDictionary(observation => observation.FolderPath);

        var createdCount = 0;

        foreach (var candidate in candidatesToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ProcessCandidateFolderAsync(
                candidate: candidate,
                observation: observationsByPath.GetValueOrDefault(candidate.FolderPath),
                settings: settings,
                localNow: localNow,
                cancellationToken: cancellationToken
            );

            if (result is CandidateFolderProcessingResult.ReleaseCreated)
            {
                createdCount++;
            }

            hasChanges |= result is not CandidateFolderProcessingResult.NothingChanged;
        }

        hasChanges |= RemoveObservationsOfFoldersThatAreNoLongerCandidates(
            observations,
            candidatesToProcess
        );

        if (hasChanges)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return createdCount;
    }

    private LocalFolderScanSettings ReadScanSettings()
    {
        var stabilityMinutes = configuration.GetValue<FolderAutomationConfiguration>(c =>
            c.StabilityMinutes
        );

        var minimumFolderSizeMegabytes = configuration.GetValue<FolderAutomationConfiguration>(c =>
            c.MinimumFolderSizeMegabytes
        );

        return new LocalFolderScanSettings(
            StabilityWindow: TimeSpan.FromMinutes(Math.Max(0, stabilityMinutes)),
            MinimumBytes: (long)Math.Max(0, minimumFolderSizeMegabytes) * 1024 * 1024
        );
    }

    private async Task<List<ReleaseFolderCandidate>> GetCandidatesWhoseFolderIsNotUsedYetAsync(
        IReadOnlySet<ReleaseFolderCandidate> candidates,
        CancellationToken cancellationToken
    )
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var candidateFolderPaths = candidates
            .Select(candidate => candidate.FolderPath)
            .Distinct()
            .ToList();

        var usedFolderPaths =
            await releaseFolderUsageRepository.GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync(
                candidateFolderPaths,
                cancellationToken
            );

        return candidates
            .Where(candidate => !usedFolderPaths.Contains(candidate.FolderPath))
            .ToList();
    }

    private async Task<HashSet<IgnoredFolderOfAutomation>> GetIgnoredFoldersAsync(
        IReadOnlyList<ReleaseFolderAutomation> automations,
        CancellationToken cancellationToken
    )
    {
        var automationIds = automations.Select(automation => automation.Id).ToList();
        var ignoredFolders = await repository.GetIgnoredFoldersAsync(
            automationIds,
            cancellationToken
        );

        return ignoredFolders
            .Select(ignoredFolder => new IgnoredFolderOfAutomation(
                ignoredFolder.ReleaseFolderAutomationId,
                ignoredFolder.FolderPath
            ))
            .ToHashSet();
    }

    private bool IgnoreCandidatesOfAutomationsInInitialScan(
        List<ReleaseFolderCandidate> candidatesWithoutUsedFolder,
        HashSet<IgnoredFolderOfAutomation> ignoredFolders,
        DateTime localNow
    )
    {
        var hasChanges = false;

        foreach (
            var candidate in candidatesWithoutUsedFolder.Where(candidate =>
                IsInInitialScanThatIgnoresExistingFolders(candidate.Automation)
            )
        )
        {
            var ignoredFolder = new IgnoredFolderOfAutomation(
                candidate.Automation.Id,
                candidate.FolderPath
            );

            if (!ignoredFolders.Add(ignoredFolder))
            {
                continue;
            }

            repository.AddIgnoredFolder(
                new ReleaseFolderAutomationIgnoredFolder
                {
                    ReleaseFolderAutomationId = candidate.Automation.Id,
                    FolderPath = candidate.FolderPath,
                    IgnoredAt = localNow,
                }
            );
            hasChanges = true;
        }

        return hasChanges;
    }

    private static bool IsInInitialScanThatIgnoresExistingFolders(
        ReleaseFolderAutomation automation
    )
    {
        return automation is { IgnoreExistingOnFirstScan: true, HasCompletedInitialScan: false };
    }

    private static bool MarkInitialScanAsCompleted(
        IReadOnlyList<ReleaseFolderAutomation> automations
    )
    {
        var automationsWithoutCompletedInitialScan = automations
            .Where(automation => !automation.HasCompletedInitialScan)
            .ToList();

        foreach (var automation in automationsWithoutCompletedInitialScan)
        {
            automation.HasCompletedInitialScan = true;
        }

        return automationsWithoutCompletedInitialScan.Count > 0;
    }

    private static List<ReleaseFolderCandidate> GetCandidatesWhoseFolderIsNotIgnored(
        List<ReleaseFolderCandidate> candidatesWithoutUsedFolder,
        HashSet<IgnoredFolderOfAutomation> ignoredFolders
    )
    {
        return candidatesWithoutUsedFolder
            .Where(candidate =>
                !ignoredFolders.Contains(
                    new IgnoredFolderOfAutomation(candidate.Automation.Id, candidate.FolderPath)
                )
            )
            .DistinctBy(candidate => candidate.FolderPath)
            .ToList();
    }

    private async Task<CandidateFolderProcessingResult> ProcessCandidateFolderAsync(
        ReleaseFolderCandidate candidate,
        ReleaseFolderObservation? observation,
        LocalFolderScanSettings settings,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var fileCountAndSize = fileSystemService.GetFolderFileCountAndSize(candidate.FolderPath);

        if (observation is null)
        {
            AddFolderObservation(candidate.FolderPath, fileCountAndSize, localNow);
            return CandidateFolderProcessingResult.ObservationChanged;
        }

        var stability = FolderStabilityCheck.GetStability(
            observed: new FolderFileCountAndSize(observation.FileCount, observation.TotalBytes),
            current: fileCountAndSize,
            lastChangedAt: observation.LastChangedAt,
            now: localNow,
            stabilityWindow: settings.StabilityWindow
        );

        if (stability is FolderStability.Changed)
        {
            ResetFolderObservationAfterFolderChanged(observation, fileCountAndSize, localNow);
            return CandidateFolderProcessingResult.ObservationChanged;
        }

        if (stability is FolderStability.NotYetStable)
        {
            return CandidateFolderProcessingResult.NothingChanged;
        }

        var shouldExtractArchives = ShouldExtractArchives(candidate.Automation);

        if (shouldExtractArchives && observation.ExtractionErrorMessage is not null)
        {
            return CandidateFolderProcessingResult.NothingChanged;
        }

        if (fileCountAndSize.TotalBytes < settings.MinimumBytes)
        {
            return CandidateFolderProcessingResult.NothingChanged;
        }

        if (
            shouldExtractArchives
            && !await verificationAndExtractionService.TryVerifyAndExtractAsync(
                observation,
                cancellationToken
            )
        )
        {
            return CandidateFolderProcessingResult.ObservationChanged;
        }

        await CreateReleaseAsync(candidate, localNow, cancellationToken);

        repository.RemoveFolderObservation(observation);

        return CandidateFolderProcessingResult.ReleaseCreated;
    }

    private void AddFolderObservation(
        string folderPath,
        FolderFileCountAndSize fileCountAndSize,
        DateTime localNow
    )
    {
        repository.AddFolderObservation(
            new ReleaseFolderObservation
            {
                FolderPath = folderPath,
                FileCount = fileCountAndSize.FileCount,
                TotalBytes = fileCountAndSize.TotalBytes,
                LastChangedAt = localNow,
            }
        );
    }

    private static void ResetFolderObservationAfterFolderChanged(
        ReleaseFolderObservation observation,
        FolderFileCountAndSize fileCountAndSize,
        DateTime localNow
    )
    {
        observation.FileCount = fileCountAndSize.FileCount;
        observation.TotalBytes = fileCountAndSize.TotalBytes;
        observation.LastChangedAt = localNow;
        observation.ExtractionErrorMessage = null;
        observation.ExtractionFailedAt = null;
    }

    private bool RemoveObservationsOfFoldersThatAreNoLongerCandidates(
        IReadOnlyList<ReleaseFolderObservation> observations,
        List<ReleaseFolderCandidate> candidatesToProcess
    )
    {
        var candidateFolderPaths = candidatesToProcess
            .Select(candidate => candidate.FolderPath)
            .ToHashSet();

        var observationsOfFoldersThatAreNoLongerCandidates = observations
            .Where(observation => !candidateFolderPaths.Contains(observation.FolderPath))
            .ToList();

        foreach (var observation in observationsOfFoldersThatAreNoLongerCandidates)
        {
            repository.RemoveFolderObservation(observation);
        }

        return observationsOfFoldersThatAreNoLongerCandidates.Count > 0;
    }

    private async Task CreateReleaseAsync(
        ReleaseFolderCandidate candidate,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var release = await releaseFromFolderCreationService.CreateAsync(
            releaseTemplate: candidate.Automation.ReleaseTemplate,
            folderPath: candidate.FolderPath,
            name: null,
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

    private static bool ShouldExtractArchives(ReleaseFolderAutomation automation)
    {
        return automation
            is {
                ExtractArchivesBeforeReleaseCreation: true,
                ReleaseTemplate.ReleaseType: ReleaseType.Managed
            };
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
                var folderPaths = await Task.Run(
                    () =>
                        candidateFinder.FindMatchingFolderPaths(
                            automation.BasePath,
                            automation.FolderNamePattern
                        ),
                    cancellationToken
                );

                return folderPaths
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

    private sealed record ReleaseFolderCandidate(
        ReleaseFolderAutomation Automation,
        string FolderPath
    );

    private sealed record IgnoredFolderOfAutomation(
        int ReleaseFolderAutomationId,
        string FolderPath
    );

    private sealed record LocalFolderScanSettings(TimeSpan StabilityWindow, long MinimumBytes);

    private enum CandidateFolderProcessingResult
    {
        NothingChanged = 1,
        ObservationChanged = 2,
        ReleaseCreated = 3,
    }
}
