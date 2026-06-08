using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record CollectionUploadSlotContainerReadModel(
    int LinkCrypterContainerId,
    string LinkCrypterRegistrationName,
    string ContainerUrl,
    LinkCrypterContainerState State,
    DateTime CreatedAt,
    int SourceUploadCount,
    IReadOnlyList<string> Errors
);
