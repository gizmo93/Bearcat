using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageHosters.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ImageHosterRegistrationWriteRepository(IBearcatWriteDbContext dbWrite)
    : IImageHosterRegistrationWriteRepository
{
    public async Task<ImageHosterRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.ImageHosterRegistrations.FirstAsync(
            registration => registration.Id == id,
            cancellationToken
        );
    }

    public void Add(ImageHosterRegistration registration)
    {
        dbWrite.ImageHosterRegistrations.Add(registration);
    }

    public void Remove(ImageHosterRegistration registration)
    {
        dbWrite.ImageHosterRegistrations.Remove(registration);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
