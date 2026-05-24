using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNfoDatabases.ReadModels;
using Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;
using Bearcat.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class NfoDatabaseRegistrationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    INfoDatabaseFactory nfoDatabaseFactory,
    ISecretProtector secretProtector
) : INfoDatabaseRegistrationReadRepository, INfoDatabaseRegistrationWriteRepository
{
    public async Task<IReadOnlyList<NfoDatabaseRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var databasesByClassName = nfoDatabaseFactory.GetByClassName();

        return await dbRead
            .NfoDatabaseRegistrations.OrderBy(registration => registration.NfoDatabaseClassName)
            .Select(registration => ToReadModel(registration, databasesByClassName))
            .ToListAsync(cancellationToken);
    }

    public async Task<NfoDatabaseRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var databasesByClassName = nfoDatabaseFactory.GetByClassName();

        return await dbRead
            .NfoDatabaseRegistrations.Where(registration => registration.Id == id)
            .Select(registration => ToReadModel(registration, databasesByClassName))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsForClassNameAsync(
        string className,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.NfoDatabaseRegistrations.AnyAsync(
            registration => registration.NfoDatabaseClassName == className,
            cancellationToken
        );
    }

    async Task<NfoDatabaseRegistration> INfoDatabaseRegistrationWriteRepository.GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.NfoDatabaseRegistrations.FirstAsync(
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
        return await dbWrite.NfoDatabaseRegistrations.AnyAsync(
            registration =>
                registration.NfoDatabaseClassName == className
                && (!excludingId.HasValue || registration.Id != excludingId.Value),
            cancellationToken
        );
    }

    public void Add(NfoDatabaseRegistration registration)
    {
        dbWrite.Add(registration);
    }

    public void Remove(NfoDatabaseRegistration registration)
    {
        dbWrite.Remove(registration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private NfoDatabaseRegistrationReadModel ToReadModel(
        NfoDatabaseRegistration registration,
        IReadOnlyDictionary<string, INfoDatabase> databasesByClassName
    )
    {
        var database = databasesByClassName[registration.NfoDatabaseClassName];
        var serializedConfig = secretProtector.Unprotect(registration.SerializedConfig);

        return new NfoDatabaseRegistrationReadModel(
            registration.Id,
            registration.IsActive,
            database.Name,
            registration.NfoDatabaseClassName,
            database.DeserializeConfig(serializedConfig).ToDictionary()
        );
    }
}
