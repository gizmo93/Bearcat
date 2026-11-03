using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportContainerReadModel(
    int Id,
    string ContainerName,
    string ContainerUrl,
    bool HasBackupLinks,
    bool IsSelected,
    LinkCrypterImportContainerStatus Status,
    LinkCrypterImportNotImportableReason? NotImportableReason,
    int? ImportedReleaseId,
    IReadOnlyList<string> ErrorMessages,
    LinkCrypterImportContainerPreview Preview
);
