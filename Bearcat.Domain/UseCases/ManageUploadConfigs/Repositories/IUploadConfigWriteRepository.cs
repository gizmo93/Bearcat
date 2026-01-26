using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;

public interface IUploadConfigWriteRepository
{
    Task<UploadConfig> GetByIdAsync(int uploadConfigId, CancellationToken cancellationToken = default);
    void Add(UploadConfig uploadConfig);
    void Remove(UploadConfig uploadConfig);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
