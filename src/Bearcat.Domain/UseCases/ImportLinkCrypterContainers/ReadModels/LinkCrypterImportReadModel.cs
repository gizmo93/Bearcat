using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportReadModel(
    int Id,
    string LinkCrypterRegistrationName,
    string ReleaseTemplateName,
    LinkCrypterImportState State,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ContainerCount,
    int ProcessedContainerCount,
    int ContainerCountToProcess
);
