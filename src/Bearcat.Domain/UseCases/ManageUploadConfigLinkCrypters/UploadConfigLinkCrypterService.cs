using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;

namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;

public class UploadConfigLinkCrypterService(IUploadConfigLinkCrypterWriteRepository repository)
{
    public async Task CreateAsync(
        int uploadConfigId,
        int linkCrypterRegistrationId,
        string? password,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfigId = uploadConfigId,
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            Password = CleanPassword(password),
            EnableCaptcha = enableCaptcha,
            EnableContainerDownload = enableContainerDownload,
            EnableClickAndLoad = enableClickAndLoad,
        };

        repository.Add(uploadConfigLinkCrypter);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var uploadConfigLinkCrypter = await repository.GetByIdAsync(id, cancellationToken);
        repository.Remove(uploadConfigLinkCrypter);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        string? password,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigLinkCrypter = await repository.GetByIdAsync(id, cancellationToken);
        uploadConfigLinkCrypter.Password = CleanPassword(password);
        uploadConfigLinkCrypter.EnableCaptcha = enableCaptcha;
        uploadConfigLinkCrypter.EnableContainerDownload = enableContainerDownload;
        uploadConfigLinkCrypter.EnableClickAndLoad = enableClickAndLoad;
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string? CleanPassword(string? password)
    {
        return string.IsNullOrWhiteSpace(password) ? null : password;
    }
}
