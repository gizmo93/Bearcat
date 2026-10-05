using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleases.Overview;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Overview;

public static class CollectionLinkContainerOverviewService
{
    public static bool HasSharedLinkCryptersOrContainers(
        CollectionUploadSlotReadModel uploadSlot
    ) => uploadSlot.SharedLinkCrypters.Count > 0 || uploadSlot.Containers.Count > 0;

    public static IReadOnlyList<CollectionUploadSlotLinkCrypterReadModel> GetSharedLinkCryptersWithoutContainer(
        CollectionUploadSlotReadModel uploadSlot
    )
    {
        var linkCrypterNamesWithContainer = uploadSlot
            .Containers.Select(container => container.LinkCrypterRegistrationName)
            .ToHashSet(StringComparer.Ordinal);

        return uploadSlot
            .SharedLinkCrypters.Where(linkCrypter =>
                !linkCrypterNamesWithContainer.Contains(linkCrypter.LinkCrypterRegistrationName)
            )
            .ToList();
    }

    public static IReadOnlyList<LinkCrypterContainerUrls> GroupCreatedContainerUrlsByLinkCrypter(
        IReadOnlyList<CollectionUploadSlotReadModel> uploadSlots
    )
    {
        return uploadSlots
            .SelectMany(uploadSlot => uploadSlot.Containers)
            .GroupBy(container => container.LinkCrypterRegistrationName)
            .Select(group => new LinkCrypterContainerUrls(
                LinkCrypterRegistrationName: group.Key,
                ContainerUrls: group
                    .Where(container =>
                        container.State is LinkCrypterContainerState.Created
                        && !string.IsNullOrWhiteSpace(container.ContainerUrl)
                    )
                    .Select(container => container.ContainerUrl)
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            ))
            .ToList();
    }
}
