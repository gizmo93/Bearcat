using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.ReadModels;

namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;

public interface IUploadConfigLinkCrypterReadRepository
{
    Task<UploadConfigLinkCrypterReadModel> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<UploadConfigLinkCrypterReadModel>> GetByUploadConfigIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<LinkCrypterOptionReadModel>> GetLinkCrypterOptionsAsync(
        CancellationToken cancellationToken = default
    );
}
