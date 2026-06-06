using Bearcat.Domain.UseCases.ManageImageHosters.ReadModels;

namespace Bearcat.Domain.UseCases.ManageImageHosters.Repositories;

public interface IImageHosterRegistrationReadRepository
{
    Task<IReadOnlyList<ImageHosterRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<ImageHosterRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );
}
