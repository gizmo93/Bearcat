using Bearcat.Abstractions.LinkCrypter.Results;

namespace Bearcat.Abstractions.LinkCrypter;

public interface ILinkCrypter
{
    string Name { get; }

    List<string> ConfigurationKeys { get; }

    bool SupportsCaptcha { get; }

    bool SupportsContainerDownload { get; }

    bool SupportsClickAndLoad { get; }

    Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyList<string> links,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    );

    Task<UpdateContainerResult> UpdateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerLink,
        string? externalReference,
        string? password,
        IReadOnlyList<string> links,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    );

    string SerializeConfig(IReadOnlyDictionary<string, string> config);

    ILinkCrypterConfig DeserializeConfig(string serializedConfig);

    Task<TryLoginResult> TryLoginAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken = default
    );
}
