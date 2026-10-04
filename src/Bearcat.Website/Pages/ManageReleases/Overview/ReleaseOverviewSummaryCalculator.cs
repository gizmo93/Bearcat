using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases.Overview;

public static class ReleaseOverviewSummaryCalculator
{
    public static ReleaseOverviewSummary Calculate(
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads
    )
    {
        var linkContainers = uploads
            .SelectMany(upload => upload.LinkCrypterLinks)
            .DistinctBy(container => container.LinkCrypterContainerId)
            .ToList();

        return new ReleaseOverviewSummary(
            uploads.Count(upload =>
                upload.UploadId is not null && upload.OnlineState is OnlineState.Online
            ),
            uploads.Count,
            linkContainers.Count(container => container.State is LinkCrypterContainerState.Created),
            linkContainers.Count,
            uploads
                .Where(upload => upload.UploadId is not null)
                .Max(upload => upload.UploadedAt ?? upload.CreatedAt),
            SummarizeArchivePasswords(uploads)
        );
    }

    public static IReadOnlyList<LinkCrypterContainerUrls> GroupCreatedContainerUrlsByLinkCrypter(
        IReadOnlyList<ReleaseOverviewUploadReadModel> uploads
    ) =>
        uploads
            .SelectMany(upload => upload.LinkCrypterLinks)
            .GroupBy(container => container.LinkCrypterRegistrationName)
            .Select(group => new LinkCrypterContainerUrls(
                group.Key,
                group
                    .Where(container =>
                        container.State is LinkCrypterContainerState.Created
                        && !string.IsNullOrWhiteSpace(container.ContainerUrl)
                    )
                    .Select(container => container.ContainerUrl)
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            ))
            .ToList();

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
            [] or [null] => new ArchivePasswordSummary(ArchivePasswordSummaryKind.None, null),
            [{ } password] => new ArchivePasswordSummary(
                ArchivePasswordSummaryKind.SamePassword,
                password
            ),
            _ => new ArchivePasswordSummary(ArchivePasswordSummaryKind.VariesPerHoster, null),
        };
    }
}
