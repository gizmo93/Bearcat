using Bearcat.Abstractions.SeriesDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageSeriesDatabases.ReadModels;
using Bearcat.Domain.UseCases.ManageSeriesDatabases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class SeriesDatabaseRegistrationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    ISeriesDatabaseFactory seriesDatabaseFactory
) : ISeriesDatabaseRegistrationReadRepository, ISeriesDatabaseRegistrationWriteRepository
{
    public async Task<IReadOnlyList<SeriesDatabaseRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var databasesByClassName = seriesDatabaseFactory.GetByClassName();

        return await dbRead
            .SeriesDatabaseRegistrations.OrderBy(registration =>
                registration.SeriesDatabaseClassName
            )
            .Select(registration => ToReadModel(registration, databasesByClassName))
            .ToListAsync(cancellationToken);
    }

    public async Task<SeriesDatabaseRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var databasesByClassName = seriesDatabaseFactory.GetByClassName();

        var registration = await dbRead
            .SeriesDatabaseRegistrations.Where(registration => registration.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return registration is null ? null : ToReadModel(registration, databasesByClassName);
    }

    public async Task<bool> ExistsForClassNameAsync(
        string className,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.SeriesDatabaseRegistrations.AnyAsync(
            registration => registration.SeriesDatabaseClassName == className,
            cancellationToken
        );
    }

    async Task<SeriesDatabaseRegistration> ISeriesDatabaseRegistrationWriteRepository.GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.SeriesDatabaseRegistrations.FirstAsync(
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
        return await dbWrite.SeriesDatabaseRegistrations.AnyAsync(
            registration =>
                registration.SeriesDatabaseClassName == className
                && (!excludingId.HasValue || registration.Id != excludingId.Value),
            cancellationToken
        );
    }

    public void Add(SeriesDatabaseRegistration registration)
    {
        dbWrite.Add(registration);
    }

    public void Remove(SeriesDatabaseRegistration registration)
    {
        dbWrite.Remove(registration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private static SeriesDatabaseRegistrationReadModel ToReadModel(
        SeriesDatabaseRegistration registration,
        IReadOnlyDictionary<string, ISeriesDatabase> databasesByClassName
    )
    {
        var database = databasesByClassName[registration.SeriesDatabaseClassName];

        return new SeriesDatabaseRegistrationReadModel(
            registration.Id,
            registration.IsActive,
            database.Name,
            registration.SeriesDatabaseClassName
        );
    }
}
