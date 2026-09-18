using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;

namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public interface IUploadReadRepository
{
    Task<PagedResult<UploadReadModel>> SearchUploadsAsync(
        UploadSearchQuery query,
        CancellationToken cancellationToken = default
    );
}
