using Bearcat.Abstractions.Security;
using Bearcat.Domain.UseCases.ResolveMediaMetadata;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class MediaMetadataResolverRepository(
    IBearcatReadDbContext dbRead,
    ISecretProtector secretProtector
) : IMediaMetadataResolverRepository
{
    public async Task<IReadOnlyList<MediaMetadataDatabaseRegistration>> GetActiveRegistrationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .MediaDatabaseRegistrations.Where(registration => registration.IsActive)
            .OrderBy(registration => registration.MediaDatabaseClassName)
            .Select(registration => new MediaMetadataDatabaseRegistration(
                registration.MediaDatabaseClassName,
                secretProtector.Unprotect(registration.SerializedConfig)
            ))
            .ToListAsync(cancellationToken);
    }
}
