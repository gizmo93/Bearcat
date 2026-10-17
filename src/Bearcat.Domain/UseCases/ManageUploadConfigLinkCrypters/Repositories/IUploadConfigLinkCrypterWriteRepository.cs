using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;

public interface IUploadConfigLinkCrypterWriteRepository
{
    Task<UploadConfigLinkCrypter> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<UploadSource> GetUploadSourceOfUploadConfigAsync(
        int uploadConfigId,
        CancellationToken cancellationToken
    );
    void Add(UploadConfigLinkCrypter uploadConfigLinkCrypter);
    void Remove(UploadConfigLinkCrypter uploadConfigLinkCrypter);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
