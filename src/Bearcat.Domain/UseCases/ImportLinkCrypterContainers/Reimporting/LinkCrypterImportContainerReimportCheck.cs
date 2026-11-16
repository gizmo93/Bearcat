using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public static class LinkCrypterImportContainerReimportCheck
{
    public static bool CanBeReimported(
        LinkCrypterImportContainerStatus status,
        int? importedReleaseId,
        LinkCrypterImportContainerPreview preview
    )
    {
        return status is LinkCrypterImportContainerStatus.Imported
            && importedReleaseId is not null
            && LinkCrypterImportMirrorSplitter.SplitByArchiveBaseName(preview.Mirrors).Count > 0;
    }

    public static bool CanBeReimported(LinkCrypterImportContainer container)
    {
        return CanBeReimported(
            status: container.Status,
            importedReleaseId: container.ImportedReleaseId,
            preview: LinkCrypterImportContainerPreview.FromJson(container.PreviewJson)
        );
    }
}
