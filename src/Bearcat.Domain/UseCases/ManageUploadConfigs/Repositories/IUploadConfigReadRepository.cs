using Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;

namespace Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;

public interface IUploadConfigReadRepository
{
    Task<IReadOnlyList<UploadConfigReadModel>> GetUploadConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<UploadConfigReadModel> GetReadModelByIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<int, string>> GetHosterRegistrationOptionsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ArchiveConfigOptionReadModel>> GetArchiveConfigOptionsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );
}
