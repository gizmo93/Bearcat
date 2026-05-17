using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadConfigReadRepository(IBearcatReadDbContext dbRead) : IUploadConfigReadRepository
{
    public async Task<IReadOnlyList<UploadConfigDto>> GetUploadConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .UploadConfigs.Where(u => u.ReleaseId == releaseId)
            .OrderBy(u => u.Id)
            .Select(u => new UploadConfigDto(
                u.Id,
                u.Name,
                u.HosterRegistration.Name,
                u.HosterRegistrationId,
                u.ArchiveConfigId,
                u.ArchiveConfig.Name,
                u.Release.Name,
                u.LinksDistributedTo
            ))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<UploadConfigDto> GetDtoByIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .UploadConfigs.Where(u => u.Id == uploadConfigId)
            .OrderBy(u => u.Id)
            .Select(u => new UploadConfigDto(
                u.Id,
                u.Name,
                u.HosterRegistration.Name,
                u.HosterRegistrationId,
                u.ArchiveConfigId,
                u.ArchiveConfig.Name,
                u.Release.Name,
                u.LinksDistributedTo
            ))
            .FirstAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetHosterRegistrationOptionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.HosterRegistrations.ToDictionaryAsync(
            h => h.Id,
            h => h.Name,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyDictionary<int, string>> GetArchiveConfigOptionsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ArchiveConfigs.Where(a => a.ReleaseId == releaseId)
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken: cancellationToken);
    }
}
