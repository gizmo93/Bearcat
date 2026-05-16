using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Dto;

namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;

public interface IUploadConfigLinkCrypterReadRepository
{
    Task<UploadConfigLinkCrypterDto> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<UploadConfigLinkCrypterDto>> GetByUploadConfigIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<int, string>> GetLinkCrypterOptionsAsync(
        CancellationToken cancellationToken = default
    );
}
