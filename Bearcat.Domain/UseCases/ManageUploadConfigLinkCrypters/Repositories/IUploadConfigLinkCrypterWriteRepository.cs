using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;

public interface IUploadConfigLinkCrypterWriteRepository
{
    Task<UploadConfigLinkCrypter> GetByIdAsync(int id, CancellationToken cancellationToken);
    void Add(UploadConfigLinkCrypter uploadConfigLinkCrypter);
    void Remove(UploadConfigLinkCrypter uploadConfigLinkCrypter);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
