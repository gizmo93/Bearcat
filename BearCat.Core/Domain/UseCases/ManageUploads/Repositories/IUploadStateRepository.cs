using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageUploads.Repositories;

public interface IUploadStateRepository
{
    Task<IReadOnlyList<Upload>> GetUploadsToCheckAsync(
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void Add(Upload upload);
}
