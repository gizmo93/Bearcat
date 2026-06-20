using Bearcat.Abstractions.ImageHoster;
using Bearcat.Domain.UseCases.ManageImageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageImageHosters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ImageHosterRegistrationReadRepository(
    IBearcatReadDbContext dbRead,
    IImageHosterFactory imageHosterFactory
) : IImageHosterRegistrationReadRepository
{
    public async Task<IReadOnlyList<ImageHosterRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var imageHostersByClassName = imageHosterFactory.GetByClassName();

        var registrations = await dbRead
            .ImageHosterRegistrations.OrderBy(registration => registration.Name)
            .Select(registration => new
            {
                registration.Id,
                registration.Name,
                registration.ImageHosterClassName,
                registration.IsActive,
            })
            .ToListAsync(cancellationToken);

        return registrations
            .Select(registration =>
            {
                var imageHoster = imageHostersByClassName[registration.ImageHosterClassName];

                return new ImageHosterRegistrationReadModel(
                    registration.Id,
                    registration.Name,
                    registration.ImageHosterClassName,
                    imageHoster.Name,
                    registration.IsActive
                );
            })
            .ToList();
    }

    public async Task<ImageHosterRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var imageHostersByClassName = imageHosterFactory.GetByClassName();

        var registration = await dbRead
            .ImageHosterRegistrations.Where(registration => registration.Id == id)
            .Select(registration => new
            {
                registration.Id,
                registration.Name,
                registration.ImageHosterClassName,
                registration.IsActive,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (registration is null)
        {
            return null;
        }

        var imageHoster = imageHostersByClassName[registration.ImageHosterClassName];

        return new ImageHosterRegistrationReadModel(
            registration.Id,
            registration.Name,
            registration.ImageHosterClassName,
            imageHoster.Name,
            registration.IsActive
        );
    }
}
