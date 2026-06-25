using Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadConfigReadRepository(IBearcatReadDbContext dbRead) : IUploadConfigReadRepository
{
    public async Task<IReadOnlyList<UploadConfigReadModel>> GetUploadConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .UploadConfigs.Where(u => u.ReleaseId == releaseId)
            .OrderBy(u => u.Id)
            .Select(u => new UploadConfigReadModel(
                u.Id,
                u.Name,
                u.HosterRegistration.Name,
                u.HosterRegistrationId,
                u.ArchiveConfigId,
                u.ArchiveConfig.Name,
                u.Release.Name,
                u.PremiumOnlyDownload
            ))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<UploadConfigReadModel> GetReadModelByIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .UploadConfigs.Where(u => u.Id == uploadConfigId)
            .OrderBy(u => u.Id)
            .Select(u => new UploadConfigReadModel(
                u.Id,
                u.Name,
                u.HosterRegistration.Name,
                u.HosterRegistrationId,
                u.ArchiveConfigId,
                u.ArchiveConfig.Name,
                u.Release.Name,
                u.PremiumOnlyDownload
            ))
            .FirstAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetHosterRegistrationOptionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .HosterRegistrations.Where(h => h.IsActive)
            .ToDictionaryAsync(h => h.Id, h => h.Name, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveConfigOptionReadModel>> GetArchiveConfigOptionsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ArchiveConfigs.Where(a => a.ReleaseId == releaseId)
            .Select(a => new ArchiveConfigOptionReadModel(a.Id, a.Name, a.ArchiveFileSizeMb))
            .ToListAsync(cancellationToken: cancellationToken);
    }
}
