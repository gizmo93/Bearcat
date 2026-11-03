using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Proxies;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers;

public class LinkCrypterCallService(
    ILinkCrypterFactory linkCrypterFactory,
    ISecretProtector secretProtector
)
{
    public async Task<CreateContainerResult> SendCreateContainerRequestAsync(
        UploadConfigLinkCrypter linkCrypterConfig,
        string containerName,
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber,
        CancellationToken cancellationToken
    )
    {
        var crypter = GetCrypter(linkCrypterConfig);
        var config = GetCrypterConfig(crypter, linkCrypterConfig);

        using var proxyScope = LinkCrypterRegistrationProxyScope.Enter(
            linkCrypterConfig.LinkCrypterRegistration
        );

        return await crypter.CreateContainerAsync(
            linkCrypterConfig: config,
            containerName: containerName,
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            linksPerMirrorNumber: linksPerMirrorNumber,
            cancellationToken: cancellationToken
        );
    }

    public async Task<UpdateContainerResult> SendUpdateContainerRequestAsync(
        UploadConfigLinkCrypter linkCrypterConfig,
        LinkCrypterContainer container,
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber,
        CancellationToken cancellationToken
    )
    {
        var crypter = GetCrypter(linkCrypterConfig);
        var config = GetCrypterConfig(crypter, linkCrypterConfig);

        using var proxyScope = LinkCrypterRegistrationProxyScope.Enter(
            linkCrypterConfig.LinkCrypterRegistration
        );

        return await crypter.UpdateContainerAsync(
            linkCrypterConfig: config,
            containerLink: container.ContainerUrl,
            externalReference: container.ExternalReference,
            password: linkCrypterConfig.Password,
            enableCaptcha: linkCrypterConfig.EnableCaptcha,
            enableContainerDownload: linkCrypterConfig.EnableContainerDownload,
            enableClickAndLoad: linkCrypterConfig.EnableClickAndLoad,
            linksPerMirrorNumber: linksPerMirrorNumber,
            cancellationToken: cancellationToken
        );
    }

    private ILinkCrypter GetCrypter(UploadConfigLinkCrypter linkCrypterConfig)
    {
        return linkCrypterFactory.Get(
            linkCrypterConfig.LinkCrypterRegistration.LinkCrypterClassName
        );
    }

    private ILinkCrypterConfig GetCrypterConfig(
        ILinkCrypter crypter,
        UploadConfigLinkCrypter linkCrypterConfig
    )
    {
        return crypter.DeserializeConfig(
            secretProtector.Unprotect(linkCrypterConfig.LinkCrypterRegistration.SerializedConfig)
        );
    }
}
