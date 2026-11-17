using Bearcat.Domain.Shared.ForumPostRendering;

namespace Bearcat.Domain.Shared.ForumPosting;

public record DistributionSiteServiceRestrictions(
    IReadOnlyList<string> RequiredHosterClassNames,
    IReadOnlyList<string> ForbiddenHosterClassNames,
    IReadOnlyList<string> RequiredLinkCrypterClassNames,
    IReadOnlyList<string> ForbiddenLinkCrypterClassNames,
    IReadOnlyList<string> RequiredImageHosterClassNames,
    IReadOnlyList<string> ForbiddenImageHosterClassNames
)
{
    public static DistributionSiteServiceRestrictions Empty { get; } = new([], [], [], [], [], []);

    public List<string> FindBlockedReasons(
        IReadOnlyList<ReleaseForumPostUploadReadModel> uploads,
        IReadOnlyList<ReleaseForumPostLinkCrypterReadModel> multiHosterLinkCrypters,
        IReadOnlyList<ForumPostImageLinkReadModel> imageLinks
    )
    {
        var hosterResult = ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
            items: uploads,
            getClassName: upload => upload.HosterClassName,
            requiredClassNames: RequiredHosterClassNames,
            forbiddenClassNames: ForbiddenHosterClassNames,
            kindName: "file hoster"
        );

        var linkCrypters = hosterResult
            .AllowedItems.SelectMany(upload => upload.LinkCrypters)
            .Concat(multiHosterLinkCrypters)
            .ToList();

        var linkCrypterResult =
            ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
                items: linkCrypters,
                getClassName: linkCrypter => linkCrypter.LinkCrypterClassName,
                requiredClassNames: RequiredLinkCrypterClassNames,
                forbiddenClassNames: ForbiddenLinkCrypterClassNames,
                kindName: "link crypter"
            );

        var imageHosterResult =
            ForumServiceRestrictionFilter.RemoveForbiddenItemsAndFindBlockedReason(
                items: imageLinks,
                getClassName: imageLink => imageLink.ImageHosterClassName,
                requiredClassNames: RequiredImageHosterClassNames,
                forbiddenClassNames: ForbiddenImageHosterClassNames,
                kindName: "image hoster"
            );

        List<string?> blockedReasons =
        [
            hosterResult.BlockedReason,
            linkCrypterResult.BlockedReason,
            imageHosterResult.BlockedReason,
        ];

        return blockedReasons.OfType<string>().ToList();
    }

    public ReleaseForumPostServiceLinks RemoveForbiddenServiceLinks(
        ReleaseForumPostServiceLinks serviceLinks
    )
    {
        var uploads = serviceLinks
            .Uploads.Where(upload => !ForbiddenHosterClassNames.Contains(upload.HosterClassName))
            .Select(upload =>
                upload with
                {
                    LinkCrypters = RemoveForbiddenLinkCrypters(upload.LinkCrypters),
                }
            )
            .ToList();

        return new ReleaseForumPostServiceLinks(
            Uploads: uploads,
            AllHostersLinkCrypters: RemoveForbiddenLinkCrypters(
                serviceLinks.AllHostersLinkCrypters
            ),
            ImageLinks: RemoveForbiddenImageLinks(serviceLinks.ImageLinks)
        );
    }

    public List<ReleaseForumPostLinkCrypterReadModel> RemoveForbiddenLinkCrypters(
        IReadOnlyList<ReleaseForumPostLinkCrypterReadModel> linkCrypters
    )
    {
        return linkCrypters
            .Where(linkCrypter =>
                !ForbiddenLinkCrypterClassNames.Contains(linkCrypter.LinkCrypterClassName)
            )
            .ToList();
    }

    public List<ForumPostImageLinkReadModel> RemoveForbiddenImageLinks(
        IReadOnlyList<ForumPostImageLinkReadModel> imageLinks
    )
    {
        return imageLinks
            .Where(imageLink =>
                !ForbiddenImageHosterClassNames.Contains(imageLink.ImageHosterClassName)
            )
            .ToList();
    }
}
