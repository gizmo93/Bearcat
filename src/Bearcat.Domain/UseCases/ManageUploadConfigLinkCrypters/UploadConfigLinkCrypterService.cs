using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using Bearcat.Domain.ValueObjects;

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
        EnsureCanManageFromUploadConfig(uploadConfigLinkCrypter);
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
        EnsureCanManageFromUploadConfig(uploadConfigLinkCrypter);
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

    private static void EnsureCanManageFromUploadConfig(
        UploadConfigLinkCrypter uploadConfigLinkCrypter
    )
    {
        if (uploadConfigLinkCrypter.ContainerScope is LinkCrypterContainerScope.ReleaseCollection)
        {
            throw new InvalidOperationException(
                "Collection scoped link crypters are managed through release templates."
            );
        }
    }
}
