using Bearcat.Abstractions.RemoteSource;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageRemoteSources.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class RemoteSourceRegistrationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IRemoteSourceFactory remoteSourceFactory
) : IRemoteSourceRegistrationReadRepository, IRemoteSourceRegistrationWriteRepository
{
    public async Task<IReadOnlyList<RemoteSourceRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var sourceNamesByClassName = remoteSourceFactory
            .GetRemoteSources()
            .ToDictionary(source => source.ClassName, source => source.Name);

        var registrations = await dbRead
            .RemoteSourceRegistrations.OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.SourceClassName,
                r.IsActive,
                r.MaxConnections,
            })
            .ToListAsync(cancellationToken);

        return registrations
            .Select(r => new RemoteSourceRegistrationReadModel(
                r.Id,
                r.Name,
                r.SourceClassName,
                sourceNamesByClassName[r.SourceClassName],
                r.IsActive,
                r.MaxConnections
            ))
            .ToList();
    }

    public async Task<RemoteSourceRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.RemoteSourceRegistrations.FirstAsync(
            r => r.Id == id,
            cancellationToken
        );
    }

    public void Add(RemoteSourceRegistration registration)
    {
        dbWrite.Add(registration);
    }

    public void Remove(RemoteSourceRegistration registration)
    {
        dbWrite.Remove(registration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
