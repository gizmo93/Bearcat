using BearCat.Core.Domain.UseCases.ManageUploadConfigs.Dto;

namespace BearCat.Core.Domain.UseCases.ManageUploadConfigs.Repositories;

public interface IUploadConfigReadRepository
{
    Task<IReadOnlyList<UploadConfigDto>> GetUploadConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken = default);

    Task<UploadConfigDto> GetDtoByIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, string>> GetHosterRegistrationOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, string>> GetArchiveConfigOptionsAsync(
        int releaseId,
        CancellationToken cancellationToken = default);
}
