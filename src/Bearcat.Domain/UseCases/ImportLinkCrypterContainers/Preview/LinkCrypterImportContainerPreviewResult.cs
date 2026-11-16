using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public sealed record LinkCrypterImportContainerPreviewResult(
    LinkCrypterImportContainerPreview Preview,
    LinkCrypterImportNotImportableReason? ContainerNotImportableReason
)
{
    public LinkCrypterImportNotImportableReason? GetNotImportableReason()
    {
        return ContainerNotImportableReason
            ?? Preview
                .Releases!.Select(releasePreview => releasePreview.NotImportableReason)
                .FirstOrDefault(notImportableReason => notImportableReason is not null);
    }
}
