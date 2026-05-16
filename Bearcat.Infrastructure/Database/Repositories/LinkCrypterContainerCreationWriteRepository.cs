using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class LinkCrypterContainerCreationWriteRepository(IBearcatWriteDbContext dbWrite)
    : ILinkCrypterContainerCreationWriteRepository
{
    public async Task<IReadOnlyList<Upload>> GetUploadsWithMissingLinkCrypterContainersAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.LinkCrypters)
                    .ThenInclude(l => l.LinkCrypterRegistration)
            .Include(u => u.LinkCrypterContainers)
            .Include(u => u.UploadedFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Uploads)
                    .ThenInclude(u => u.LinkCrypterContainers)
            .Where(u =>
                u.UploadState == UploadState.Completed
                && u.OnlineState == OnlineState.Online
                && u.UploadedFiles.Any()
                && u.UploadConfig.LinkCrypters.Any(l =>
                    l.LinkCrypterRegistration.IsActive
                    && !u
                        .LinkCrypterContainers.Select(c => c.UploadConfigLinkCrypterId)
                        .Contains(l.Id)
                )
            )
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public void Add(LinkCrypterContainer container)
    {
        dbWrite.Add(container);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
