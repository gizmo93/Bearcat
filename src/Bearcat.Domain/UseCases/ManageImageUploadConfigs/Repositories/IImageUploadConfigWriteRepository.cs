using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;

public interface IImageUploadConfigWriteRepository
{
    Task<ImageUploadConfig> GetByIdAsync(
        int imageUploadConfigId,
        CancellationToken cancellationToken = default
    );

    void Add(ImageUploadConfig imageUploadConfig);

    void Remove(ImageUploadConfig imageUploadConfig);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
