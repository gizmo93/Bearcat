using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportDetailReadModel(
    int Id,
    int LinkCrypterRegistrationId,
    string LinkCrypterRegistrationName,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    string? ArchiveFilesBasePathForReleasesWithoutLocalFiles,
    string? PrimaryLanguageCode,
    LinkCrypterImportState State,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ProcessedContainerCount,
    int ContainerCountToProcess,
    int SelectedContainerCount,
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyList<LinkCrypterImportContainerStatusCountReadModel> ContainerCountsByStatus,
    IReadOnlyList<LinkCrypterImportHosterRegistrationReadModel> HosterRegistrations
);
