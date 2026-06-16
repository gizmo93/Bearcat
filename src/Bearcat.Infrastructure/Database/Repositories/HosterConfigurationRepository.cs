using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class HosterConfigurationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IHosterFactory hosterFactory
) : IHosterConfigurationReadRepository, IHosterConfigurationWriteRepository
{
    public async Task<IReadOnlyList<HosterRegistrationReadModel>> GetAllRegistrationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var hostersByName = hosterFactory.GetHostersByName();

        var registrations = await dbRead
            .HosterRegistrations.AsNoTracking()
            .OrderBy(h => h.Name)
            .Select(h => new
            {
                h.Id,
                h.Name,
                h.IsActive,
                h.RequiresCaptchaVerification,
                h.HosterClassName,
                h.MaxParallelUploadsOverride,
            })
            .ToListAsync(cancellationToken: cancellationToken);

        return registrations
            .Select(h =>
            {
                var hoster = hostersByName[h.HosterClassName];

                return new HosterRegistrationReadModel(
                    h.Id,
                    h.Name,
                    h.IsActive,
                    h.RequiresCaptchaVerification,
                    hoster is IHosterWithCaptchaVerification,
                    hoster.SupportsPremiumOnlyDownloads,
                    hoster.HasFixedParallelUploadLimit,
                    hoster.DefaultMaximumParallelUploads,
                    h.MaxParallelUploadsOverride,
                    hoster.Name,
                    h.HosterClassName
                );
            })
            .ToList();
    }

    public async Task<HosterRegistration> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbWrite.HosterRegistrations.FirstAsync(h => h.Id == id, cancellationToken);
    }

    public void Add(HosterRegistration registration)
    {
        dbWrite.Add(registration);
    }

    public void Remove(HosterRegistration registration)
    {
        dbWrite.Remove(registration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
