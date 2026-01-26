using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;

namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;

public class UploadConfigLinkCrypterService(IUploadConfigLinkCrypterWriteRepository repository)
{
    public async Task CreateAsync(
        int uploadConfigId,
        int linkCrypterRegistrationId,
        string containerName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var uploadConfigLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfigId = uploadConfigId,
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ContainerName = containerName,
            Password = password,
        };
        
        repository.Add(uploadConfigLinkCrypter);
        await repository.SaveChangesAsync(cancellationToken);
    }
    
    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var uploadConfigLinkCrypter = await repository.GetByIdAsync(id, cancellationToken);
        repository.Remove(uploadConfigLinkCrypter);
        await repository.SaveChangesAsync(cancellationToken);
    }
    
    public async Task UpdateAsync(
        int id,
        string containerName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var uploadConfigLinkCrypter = await repository.GetByIdAsync(id, cancellationToken);
        uploadConfigLinkCrypter.ContainerName = containerName;
        uploadConfigLinkCrypter.Password = password;
        await repository.SaveChangesAsync(cancellationToken);
    }
}
