using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;

namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;

public class ReleaseFolderAutomationService(IReleaseFolderAutomationWriteRepository repository)
{
    public async Task<int> CreateAsync(
        string basePath,
        string? folderNamePattern,
        int releaseTemplateId,
        string? primaryLanguageCode,
        bool extractArchivesBeforeReleaseCreation,
        bool isEnabled,
        bool ignoreExistingOnFirstScan,
        CancellationToken cancellationToken = default
    )
    {
        var automation = new ReleaseFolderAutomation
        {
            BasePath = basePath.Trim(),
            FolderNamePattern = string.IsNullOrWhiteSpace(folderNamePattern)
                ? null
                : folderNamePattern.Trim(),
            ReleaseTemplateId = releaseTemplateId,
            PrimaryLanguageCode = string.IsNullOrWhiteSpace(primaryLanguageCode)
                ? null
                : primaryLanguageCode.Trim().ToLowerInvariant(),
            ExtractArchivesBeforeReleaseCreation = extractArchivesBeforeReleaseCreation,
            IsEnabled = isEnabled,
            IgnoreExistingOnFirstScan = ignoreExistingOnFirstScan,
            HasCompletedInitialScan = false,
        };

        repository.Add(automation);
        await repository.SaveChangesAsync(cancellationToken);

        return automation.Id;
    }

    public async Task UpdateAsync(
        int releaseFolderAutomationId,
        string basePath,
        string? folderNamePattern,
        int releaseTemplateId,
        string? primaryLanguageCode,
        bool extractArchivesBeforeReleaseCreation,
        bool isEnabled,
        bool ignoreExistingOnFirstScan,
        CancellationToken cancellationToken = default
    )
    {
        var automation = await repository.GetByIdAsync(
            releaseFolderAutomationId,
            cancellationToken
        );

        var normalizedBasePath = basePath.Trim();
        var normalizedFolderNamePattern = string.IsNullOrWhiteSpace(folderNamePattern)
            ? null
            : folderNamePattern.Trim();

        if (
            automation.BasePath != normalizedBasePath
            || automation.FolderNamePattern != normalizedFolderNamePattern
        )
        {
            automation.HasCompletedInitialScan = false;
        }

        automation.BasePath = normalizedBasePath;
        automation.FolderNamePattern = normalizedFolderNamePattern;

        automation.ReleaseTemplateId = releaseTemplateId;

        automation.PrimaryLanguageCode = string.IsNullOrWhiteSpace(primaryLanguageCode)
            ? null
            : primaryLanguageCode.Trim().ToLowerInvariant();

        automation.ExtractArchivesBeforeReleaseCreation = extractArchivesBeforeReleaseCreation;
        automation.IsEnabled = isEnabled;
        automation.IgnoreExistingOnFirstScan = ignoreExistingOnFirstScan;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(
        int releaseFolderAutomationId,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var automation = await repository.GetByIdAsync(
            releaseFolderAutomationId,
            cancellationToken
        );

        automation.IsEnabled = isEnabled;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryExtractionAsync(
        int releaseFolderObservationId,
        CancellationToken cancellationToken = default
    )
    {
        var observation = await repository.GetObservationByIdAsync(
            releaseFolderObservationId,
            cancellationToken
        );

        observation.ExtractionErrorMessage = null;
        observation.ExtractionFailedAt = null;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int releaseFolderAutomationId,
        CancellationToken cancellationToken = default
    )
    {
        var automation = await repository.GetByIdAsync(
            releaseFolderAutomationId,
            cancellationToken
        );

        repository.Remove(automation);

        await repository.SaveChangesAsync(cancellationToken);
    }
}
