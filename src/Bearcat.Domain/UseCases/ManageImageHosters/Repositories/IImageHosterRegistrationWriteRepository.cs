using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageImageHosters.Repositories;

public interface IImageHosterRegistrationWriteRepository
{
    Task<ImageHosterRegistration> GetByIdAsync(int id, CancellationToken cancellationToken);

    void Add(ImageHosterRegistration registration);

    void Remove(ImageHosterRegistration registration);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
