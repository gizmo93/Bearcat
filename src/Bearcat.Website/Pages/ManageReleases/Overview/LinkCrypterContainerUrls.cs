namespace Bearcat.Website.Pages.ManageReleases.Overview;

public record LinkCrypterContainerUrls(
    string LinkCrypterRegistrationName,
    IReadOnlyList<string> ContainerUrls
);
