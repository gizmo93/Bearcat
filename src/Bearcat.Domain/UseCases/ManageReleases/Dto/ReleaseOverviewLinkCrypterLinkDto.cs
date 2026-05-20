using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseOverviewLinkCrypterLinkDto(
    int LinkCrypterContainerId,
    string LinkCrypterRegistrationName,
    string LinkCrypterClassName,
    string ContainerUrl,
    LinkCrypterContainerState State,
    DateTime CreatedAt
);
