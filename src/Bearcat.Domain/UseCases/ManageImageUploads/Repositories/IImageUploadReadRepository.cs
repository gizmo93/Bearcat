using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;

namespace Bearcat.Domain.UseCases.ManageImageUploads.Repositories;

public interface IImageUploadReadRepository
{
    Task<IReadOnlyList<ReleaseImageUploadReadModel>> GetImageUploadsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseImageUploadUrlReadModel>> GetImageUploadUrlsAsync(
        int releaseId,
        int imageUploadId,
        CancellationToken cancellationToken = default
    );
}
