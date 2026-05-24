using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseOverviewLinkCrypterLinkReadModel(
    int LinkCrypterContainerId,
    string LinkCrypterRegistrationName,
    string LinkCrypterClassName,
    string ContainerUrl,
    LinkCrypterContainerState State,
    DateTime CreatedAt,
    IReadOnlyList<string> Errors
);
