using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.Repositories;

namespace Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;

public class RemoteSourceAutomationService(IRemoteSourceAutomationWriteRepository repository)
{
    public const int MinPriority = 0;

    public async Task<int> CreateAsync(
        RemoteSourceAutomationInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = await NormalizeAsync(input, cancellationToken);

        var automation = new RemoteSourceAutomation
        {
            Name = normalized.Name,
            RemoteSourceRegistrationId = normalized.RemoteSourceRegistrationId,
            RemotePath = normalized.RemotePath,
            TargetPath = normalized.TargetPath,
            FolderNamePattern = normalized.FolderNamePattern,
            ReleaseTemplateId = normalized.ReleaseTemplateId,
            PrimaryLanguageCode = normalized.PrimaryLanguageCode,
            KeepRawFiles = normalized.KeepRawFiles,
            Priority = normalized.Priority,
            IsEnabled = true,
            IgnoreExistingOnFirstScan = normalized.IgnoreExistingOnFirstScan,
            HasCompletedInitialScan = false,
        };

        repository.Add(automation);
        await repository.SaveChangesAsync(cancellationToken);

        return automation.Id;
    }

    public async Task UpdateAsync(
        int id,
        RemoteSourceAutomationInput input,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = await NormalizeAsync(input, cancellationToken);
        var automation = await repository.GetByIdAsync(id, cancellationToken);

        if (
            automation.RemoteSourceRegistrationId != normalized.RemoteSourceRegistrationId
            || automation.RemotePath != normalized.RemotePath
        )
        {
            automation.HasCompletedInitialScan = false;
        }

        automation.Name = normalized.Name;
        automation.RemoteSourceRegistrationId = normalized.RemoteSourceRegistrationId;
        automation.RemotePath = normalized.RemotePath;
        automation.TargetPath = normalized.TargetPath;
        automation.FolderNamePattern = normalized.FolderNamePattern;
        automation.ReleaseTemplateId = normalized.ReleaseTemplateId;
        automation.PrimaryLanguageCode = normalized.PrimaryLanguageCode;
        automation.KeepRawFiles = normalized.KeepRawFiles;
        automation.Priority = normalized.Priority;
        automation.IgnoreExistingOnFirstScan = normalized.IgnoreExistingOnFirstScan;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(
        int id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var automation = await repository.GetByIdAsync(id, cancellationToken);
        automation.IsEnabled = isEnabled;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var automation = await repository.GetByIdAsync(id, cancellationToken);
        var observingDownloads = await repository.GetObservingDownloadsAsync(id, cancellationToken);

        foreach (var download in observingDownloads)
        {
            repository.Remove(download);
        }

        repository.Remove(automation);

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<RemoteSourceAutomationInput> NormalizeAsync(
        RemoteSourceAutomationInput input,
        CancellationToken cancellationToken
    )
    {
        var normalized = input with
        {
            Name = NormalizeName(input.Name),
            RemotePath = RemotePathNormalizer.Normalize(input.RemotePath),
            TargetPath = NormalizeTargetPath(input.TargetPath),
            FolderNamePattern = string.IsNullOrWhiteSpace(input.FolderNamePattern)
                ? null
                : input.FolderNamePattern.Trim(),
            PrimaryLanguageCode = string.IsNullOrWhiteSpace(input.PrimaryLanguageCode)
                ? null
                : input.PrimaryLanguageCode.Trim().ToLowerInvariant(),
        };

        ValidatePriority(normalized.Priority);
        await ValidateReferencesAsync(normalized, cancellationToken);

        return normalized;
    }

    private async Task ValidateReferencesAsync(
        RemoteSourceAutomationInput input,
        CancellationToken cancellationToken
    )
    {
        if (
            !await repository.RegistrationExistsAsync(
                input.RemoteSourceRegistrationId,
                cancellationToken
            )
        )
        {
            throw new ArgumentException(
                "The selected remote source does not exist.",
                nameof(input)
            );
        }

        if (
            !await repository.ReleaseTemplateExistsAsync(input.ReleaseTemplateId, cancellationToken)
        )
        {
            throw new ArgumentException(
                "The selected release template does not exist.",
                nameof(input)
            );
        }
    }

    private static void ValidatePriority(int priority)
    {
        if (priority < MinPriority)
        {
            throw new ArgumentOutOfRangeException(
                nameof(priority),
                priority,
                $"Priority must be at least {MinPriority}."
            );
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return name.Trim();
    }

    private static string NormalizeTargetPath(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new ArgumentException("Target path is required.", nameof(targetPath));
        }

        var trimmedTargetPath = targetPath.Trim();

        if (!Path.IsPathFullyQualified(trimmedTargetPath))
        {
            throw new ArgumentException(
                "Target path must be an absolute path.",
                nameof(targetPath)
            );
        }

        return trimmedTargetPath;
    }
}
