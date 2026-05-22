using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseUploadContainerLinkReadModel(
    string LinkCrypterRegistrationName,
    string LinkCrypterClassName,
    string ContainerUrl,
    LinkCrypterContainerState State,
    DateTime CreatedAt
);
