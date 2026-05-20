using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseUploadContainerLinkDto(
    string LinkCrypterRegistrationName,
    string LinkCrypterClassName,
    string ContainerUrl,
    LinkCrypterContainerState State,
    DateTime CreatedAt
);
