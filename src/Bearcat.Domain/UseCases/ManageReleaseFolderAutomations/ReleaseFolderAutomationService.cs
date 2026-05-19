using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;

namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;

public class ReleaseFolderAutomationService(IReleaseFolderAutomationWriteRepository repository)
{
    public async Task<int> CreateAsync(
        string basePath,
        string? folderNamePattern,
        int releaseTemplateId,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var automation = new ReleaseFolderAutomation
        {
            BasePath = CleanRequired(basePath),
            FolderNamePattern = CleanOptional(folderNamePattern),
            ReleaseTemplateId = releaseTemplateId,
            IsEnabled = isEnabled,
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
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var automation = await repository.GetByIdAsync(
            releaseFolderAutomationId,
            cancellationToken
        );

        automation.BasePath = CleanRequired(basePath);
        automation.FolderNamePattern = CleanOptional(folderNamePattern);
        automation.ReleaseTemplateId = releaseTemplateId;
        automation.IsEnabled = isEnabled;

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

    private static string CleanRequired(string value)
    {
        return value.Trim();
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
