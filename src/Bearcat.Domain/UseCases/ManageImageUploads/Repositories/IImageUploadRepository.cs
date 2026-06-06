using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageImageUploads.Repositories;

public interface IImageUploadRepository
{
    Task CreateMissingImageUploadsAsync(
        DateTime createdAt,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ImageUpload>> GetPendingImageUploadsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyDictionary<int, string>> GetConfigByImageHosterRegistrationIdAsync(
        CancellationToken cancellationToken = default
    );

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
