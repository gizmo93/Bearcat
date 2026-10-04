using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases.Overview;

public static class ReleaseOverviewSummaryService
{
    public static ReleaseOverviewSummary BuildReleaseOverviewSummary(
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads
    )
    {
        var linkContainers = uploads
            .SelectMany(upload => upload.LinkCrypterLinks)
            .DistinctBy(container => container.LinkCrypterContainerId)
            .ToList();

        return new ReleaseOverviewSummary(
            OnlineHosterCount: uploads.Count(upload =>
                upload.UploadId is not null && upload.OnlineState is OnlineState.Online
            ),
            HosterCount: uploads.Count,
            CreatedLinkContainerCount: linkContainers.Count(container =>
                container.State is LinkCrypterContainerState.Created
            ),
            LinkContainerCount: linkContainers.Count,
            LatestUploadAt: uploads
                .Where(upload => upload.UploadId is not null)
                .Max(upload => upload.UploadedAt ?? upload.CreatedAt),
            ArchivePassword: SummarizeArchivePasswords(uploads)
        );
    }

    public static IReadOnlyList<LinkCrypterContainerUrls> GroupCreatedContainerUrlsByLinkCrypter(
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads
    )
    {
        return uploads
            .SelectMany(upload => upload.LinkCrypterLinks)
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

    private static ArchivePasswordSummary SummarizeArchivePasswords(
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads
    )
    {
        var passwords = uploads
            .Where(upload => upload.UploadId is not null)
            .Select(upload =>
                string.IsNullOrWhiteSpace(upload.ArchivePassword) ? null : upload.ArchivePassword
            )
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return passwords switch
        {
            [] or [null] => new ArchivePasswordSummary(
                Kind: ArchivePasswordSummaryKind.None,
                Password: null
            ),
            [{ } password] => new ArchivePasswordSummary(
                Kind: ArchivePasswordSummaryKind.SamePassword,
                Password: password
            ),
            _ => new ArchivePasswordSummary(
                Kind: ArchivePasswordSummaryKind.VariesPerHoster,
                Password: null
            ),
        };
    }
}
