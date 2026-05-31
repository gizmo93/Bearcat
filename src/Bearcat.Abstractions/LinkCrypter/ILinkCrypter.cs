using Bearcat.Abstractions.LinkCrypter.Results;

namespace Bearcat.Abstractions.LinkCrypter;

public interface ILinkCrypter
{
    string Name { get; }

    List<string> ConfigurationKeys { get; }

    Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken = default
    );

    Task<UpdateContainerResult> UpdateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerLink,
        string? externalReference,
        string? password,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken = default
    );

    string SerializeConfig(IReadOnlyDictionary<string, string> config);

    ILinkCrypterConfig DeserializeConfig(string serializedConfig);

    Task<TryLoginResult> TryLoginAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken = default
    );
}
