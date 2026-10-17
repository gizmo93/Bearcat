using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.UploadConfigLinkCrypterTemplates;

public class UploadConfigLinkCrypterTemplateService(IReleaseTemplateWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int uploadConfigTemplateId,
        int linkCrypterRegistrationId,
        string? password,
        bool enableCaptcha,
        bool enableContainerDownload,
        bool enableClickAndLoad,
        LinkCrypterContainerScope containerScope = LinkCrypterContainerScope.Release,
        CancellationToken cancellationToken = default
    )
    {
        var uploadConfigTemplate = await writeRepository.GetUploadConfigTemplateAsync(
            uploadConfigTemplateId,
            cancellationToken
        );

        if (uploadConfigTemplate.UploadSource == UploadSource.VideoFiles)
        {
            throw new InvalidUploadSourceException(
                "Link crypters cannot be added to video upload configuration templates."
            );
        }

        var linkCrypterTemplate = new UploadConfigLinkCrypterTemplate
        {
            LinkCrypterRegistrationId = linkCrypterRegistrationId,
            ContainerScope = containerScope,
            Password = password.TrimOrNullWhenWhitespace(),
            EnableCaptcha = enableCaptcha,
            EnableContainerDownload = enableContainerDownload,
            EnableClickAndLoad = enableClickAndLoad,
        };

        uploadConfigTemplate.LinkCrypterTemplates.Add(linkCrypterTemplate);

        await writeRepository.SaveChangesAsync(cancellationToken);

        return linkCrypterTemplate.Id;
    }

    public async Task UpdateAsync(
        int uploadConfigLinkCrypterTemplateId,
        string? password,
        bool enableCaptcha,
        bool enableContainerDownload,
        bool enableClickAndLoad,
        LinkCrypterContainerScope containerScope = LinkCrypterContainerScope.Release,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterTemplate = await writeRepository.GetUploadConfigLinkCrypterTemplateAsync(
            uploadConfigLinkCrypterTemplateId,
            cancellationToken
        );

        linkCrypterTemplate.Password = password.TrimOrNullWhenWhitespace();
        linkCrypterTemplate.ContainerScope = containerScope;
        linkCrypterTemplate.EnableCaptcha = enableCaptcha;
        linkCrypterTemplate.EnableContainerDownload = enableContainerDownload;
        linkCrypterTemplate.EnableClickAndLoad = enableClickAndLoad;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int uploadConfigLinkCrypterTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var linkCrypterTemplate = await writeRepository.GetUploadConfigLinkCrypterTemplateAsync(
            uploadConfigLinkCrypterTemplateId,
            cancellationToken
        );

        writeRepository.Remove(linkCrypterTemplate);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}
