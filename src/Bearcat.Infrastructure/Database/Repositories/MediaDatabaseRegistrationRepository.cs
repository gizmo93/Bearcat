using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageMediaDatabases.ReadModels;
using Bearcat.Domain.UseCases.ManageMediaDatabases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class MediaDatabaseRegistrationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IMediaMetadataDatabaseFactory metadataDatabaseFactory
) : IMediaDatabaseRegistrationReadRepository, IMediaDatabaseRegistrationWriteRepository
{
    public async Task<IReadOnlyList<MediaDatabaseRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var databasesByClassName = metadataDatabaseFactory.GetByClassName();

        return await dbRead
            .MediaDatabaseRegistrations.OrderBy(registration => registration.MediaDatabaseClassName)
            .Select(registration => ToReadModel(registration, databasesByClassName))
            .ToListAsync(cancellationToken);
    }

    public async Task<MediaDatabaseRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var databasesByClassName = metadataDatabaseFactory.GetByClassName();

        var registration = await dbRead
            .MediaDatabaseRegistrations.Where(registration => registration.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return registration is null ? null : ToReadModel(registration, databasesByClassName);
    }

    public async Task<bool> ExistsForClassNameAsync(
        string className,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.MediaDatabaseRegistrations.AnyAsync(
            registration => registration.MediaDatabaseClassName == className,
            cancellationToken
        );
    }

    async Task<MediaDatabaseRegistration> IMediaDatabaseRegistrationWriteRepository.GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.MediaDatabaseRegistrations.FirstAsync(
            registration => registration.Id == id,
            cancellationToken
        );
    }

    public async Task<bool> ExistsForClassNameAsync(
        string className,
        int? excludingId = null,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.MediaDatabaseRegistrations.AnyAsync(
            registration =>
                registration.MediaDatabaseClassName == className
                && (!excludingId.HasValue || registration.Id != excludingId.Value),
            cancellationToken
        );
    }

    public void Add(MediaDatabaseRegistration registration)
    {
        dbWrite.Add(registration);
    }

    public void Remove(MediaDatabaseRegistration registration)
    {
        dbWrite.Remove(registration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private static MediaDatabaseRegistrationReadModel ToReadModel(
        MediaDatabaseRegistration registration,
        IReadOnlyDictionary<string, IMediaMetadataDatabase> databasesByClassName
    )
    {
        var database = databasesByClassName[registration.MediaDatabaseClassName];

        return new MediaDatabaseRegistrationReadModel(
            registration.Id,
            registration.IsActive,
            database.Name,
            registration.MediaDatabaseClassName
        );
    }
}
