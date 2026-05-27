using System.IO.Enumeration;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class AutomaticallyCreateReleasesService(
    IAutomaticallyCreateReleasesRepository repository,
    IFileSystemService fileSystemService,
    ReleaseInfoResolutionService releaseInfoResolutionService,
    TimeProvider timeProvider,
    IArchiverFactory archiverFactory
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
        if (candidates.Count == 0)
        {
            return 0;
        }

        var existingReleaseFolderPaths = await repository.GetExistingReleaseFolderPathsAsync(
            candidates.Select(candidate => candidate.FolderPath).Distinct().ToList(),
            cancellationToken
        );

        var createdCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var created = await CreateReleaseAsync(
                candidate: candidate,
                existingReleaseFolderPaths: existingReleaseFolderPaths,
                cancellationToken: cancellationToken
            );

            if (created)
            {
                createdCount++;
            }
        }

        if (createdCount > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return createdCount;
    }

    private async Task<bool> CreateReleaseAsync(
        ReleaseFolderCandidate candidate,
        HashSet<string> existingReleaseFolderPaths,
        CancellationToken cancellationToken
    )
    {
        if (!existingReleaseFolderPaths.Add(candidate.FolderPath))
        {
            return false;
        }

        var localNow = timeProvider.GetLocalNow();

        var release = ReleaseService.CreateFromTemplate(
            releaseTemplate: candidate.Automation.ReleaseTemplate,
            releaseFolderPath: candidate.FolderPath,
            name: null,
            releaseType: candidate.Automation.ReleaseTemplate.ReleaseType,
            archivers: candidate.Automation.ReleaseTemplate.ReleaseType is ReleaseType.Unmanaged
                ? archiverFactory.GetArchivers()
                : [],
            localNow: localNow
        );

        release.CreatedAt = localNow;

        await releaseInfoResolutionService.TryResolveAsync(release, cancellationToken);

        repository.Add(release);
        repository.Add(
            new Notification
            {
                CreatedAt = timeProvider.GetLocalNow(),
                NotificationType = NotificationType.Info,
                Message =
                    $"Release '{release.Name}' was created automatically from template '{candidate.Automation.ReleaseTemplate.Name}'.",
            }
        );

        return true;
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
            GetFolderName(folderPath),
            ignoreCase: true
        );
    }

    private static string GetFolderName(string folderPath)
    {
        var normalizedPath = folderPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        return Path.GetFileName(normalizedPath);
    }

    private sealed record ReleaseFolderCandidate(
        ReleaseFolderAutomation Automation,
        string FolderPath
    );
}
