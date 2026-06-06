using Bearcat.Domain.UseCases.ManageImageUploadConfigs.ReadModels;

namespace Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;

public interface IImageUploadConfigReadRepository
{
    Task<IReadOnlyList<ImageUploadConfigReadModel>> GetImageUploadConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<ImageUploadConfigReadModel> GetReadModelByIdAsync(
        int imageUploadConfigId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<int, string>> GetImageHosterRegistrationOptionsAsync(
        CancellationToken cancellationToken = default
    );
}
