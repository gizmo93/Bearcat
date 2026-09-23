using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning.Repositories;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class RemoteSourceAutomationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
)
    : IRemoteSourceAutomationReadRepository,
        IRemoteSourceAutomationWriteRepository,
        IRemoteSourceScanRepository
{
    public async Task<IReadOnlyList<RemoteSourceAutomationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var automations = await dbRead
            .RemoteSourceAutomations.OrderBy(automation => automation.RemoteSourceRegistration.Name)
            .ThenBy(automation => automation.RemoteSourceRegistrationId)
            .ThenBy(automation => automation.Priority)
            .ThenBy(automation => automation.Id)
            .Select(automation => new
            {
                automation.Id,
                automation.Name,
                automation.RemoteSourceRegistrationId,
                RemoteSourceRegistrationName = automation.RemoteSourceRegistration.Name,
                IsRemoteSourceRegistrationActive = automation.RemoteSourceRegistration.IsActive,
                automation.RemotePath,
                automation.TargetPath,
                automation.FolderNamePattern,
                automation.ReleaseTemplateId,
                ReleaseTemplateName = automation.ReleaseTemplate.Name,
                automation.ReleaseTemplate.ReleaseType,
                automation.PrimaryLanguageCode,
                automation.KeepRawFiles,
                automation.Priority,
                automation.IsEnabled,
                automation.IgnoreExistingOnFirstScan,
            })
            .ToListAsync(cancellationToken);

        var downloadCounts = await dbRead
            .RemoteSourceDownloads.Where(download => download.RemoteSourceAutomationId != null)
            .GroupBy(download => new { download.RemoteSourceAutomationId, download.State })
            .Select(group => new
            {
                RemoteSourceAutomationId = group.Key.RemoteSourceAutomationId!.Value,
                group.Key.State,
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var downloadCountsByAutomationId = downloadCounts
            .GroupBy(count => count.RemoteSourceAutomationId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(count => count.State, count => count.Count)
            );

        return automations
            .Select(automation => new RemoteSourceAutomationReadModel(
                automation.Id,
                automation.Name,
                automation.RemoteSourceRegistrationId,
                automation.RemoteSourceRegistrationName,
                automation.IsRemoteSourceRegistrationActive,
                automation.RemotePath,
                automation.TargetPath,
                automation.FolderNamePattern,
                automation.ReleaseTemplateId,
                automation.ReleaseTemplateName,
                automation.ReleaseType,
                automation.PrimaryLanguageCode,
                automation.KeepRawFiles,
                automation.Priority,
                automation.IsEnabled,
                automation.IgnoreExistingOnFirstScan,
                downloadCountsByAutomationId.GetValueOrDefault(automation.Id)
                    ?? new Dictionary<RemoteSourceDownloadState, int>()
            ))
            .ToList();
    }

    public async Task<RemoteSourceAutomation> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.RemoteSourceAutomations.FirstAsync(
            automation => automation.Id == id,
            cancellationToken
        );
    }

    public async Task<bool> RegistrationExistsAsync(
        int remoteSourceRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.RemoteSourceRegistrations.AnyAsync(
            registration => registration.Id == remoteSourceRegistrationId,
            cancellationToken
        );
    }

    public async Task<bool> ReleaseTemplateExistsAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ReleaseTemplates.AnyAsync(
            template => template.Id == releaseTemplateId,
            cancellationToken
        );
    }

    public async Task<
        IReadOnlyList<RemoteSourceAutomation>
    > GetEnabledAutomationsWithActiveRegistrationAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .RemoteSourceAutomations.Where(automation =>
                automation.IsEnabled && automation.RemoteSourceRegistration.IsActive
            )
            .Include(automation => automation.RemoteSourceRegistration)
            .Include(automation => automation.ReleaseTemplate)
            .OrderBy(automation => automation.RemoteSourceRegistrationId)
            .ThenBy(automation => automation.Priority)
            .ThenBy(automation => automation.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteSourceDownload>> GetDownloadsAsync(
        int remoteSourceRegistrationId,
        IReadOnlyList<string> remoteFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .RemoteSourceDownloads.Where(download =>
                download.RemoteSourceRegistrationId == remoteSourceRegistrationId
                && remoteFolderPaths.Contains(download.RemoteFolderPath)
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteSourceDownload>> GetObservingDownloadsAsync(
        int remoteSourceAutomationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .RemoteSourceDownloads.Where(download =>
                download.RemoteSourceAutomationId == remoteSourceAutomationId
                && download.State == RemoteSourceDownloadState.Observing
            )
            .ToListAsync(cancellationToken);
    }

    public void Add(RemoteSourceAutomation automation)
    {
        dbWrite.Add(automation);
    }

    public void Add(RemoteSourceDownload download)
    {
        dbWrite.Add(download);
    }

    public void Remove(RemoteSourceAutomation automation)
    {
        dbWrite.Remove(automation);
    }

    public void Remove(RemoteSourceDownload download)
    {
        dbWrite.Remove(download);
    }

    public void DiscardPendingChanges()
    {
        foreach (var entry in dbWrite.ChangeTracker.Entries().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;
                case EntityState.Modified:
                case EntityState.Deleted:
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                    break;
            }
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
