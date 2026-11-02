using Bearcat.Abstractions.LinkCrypter.Results;

namespace Bearcat.Abstractions.LinkCrypter;

public interface ILinkCrypterWithContainerReading : ILinkCrypter
{
    Task<ListContainersResult> ListContainersAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken
    );

    Task<ReadContainerLinksResult> ReadContainerLinksAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerUrl,
        string? externalReference,
        CancellationToken cancellationToken
    );
}
