using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;

namespace Bearcat.Domain.UseCases.RunUploadCompletionActions;

public class UploadCompletionActionInputBuilder(
    ReleaseForumPostRenderModelBuilder releaseForumPostRenderModelBuilder,
    IUploadCompletionActionRepository repository,
    IImageHosterFactory imageHosterFactory,
    ILinkCrypterFactory linkCrypterFactory
)
{
    public async Task<UploadCompletionActionInput> BuildAsync(
        UploadCompletionActionDueRelease dueRelease,
        CancellationToken cancellationToken
    )
    {
        var release = await repository.GetReleaseAsync(dueRelease.ReleaseId, cancellationToken);

        var serviceLinksByReleaseId =
            await releaseForumPostRenderModelBuilder.LoadServiceLinksAsync(
                [dueRelease.ReleaseId],
                cancellationToken
            );

        var serviceLinks = serviceLinksByReleaseId[dueRelease.ReleaseId];

        var allHostersLinkCrypterContainers = serviceLinks
            .AllHostersLinkCrypters.Select(ToLinkCrypterContainer)
            .ToList();

        var allHostersContainerUrls = allHostersLinkCrypterContainers
            .Select(container => container.Url)
            .ToHashSet();

        var mirrors = serviceLinks
            .Uploads.Where(upload => upload.Links.Count > 0)
            .Select(upload => ToMirror(upload, allHostersContainerUrls))
            .ToList();

        return new UploadCompletionActionInput(
            Release: release,
            IsReupload: dueRelease.IsReupload,
            Mirrors: mirrors,
            AllHostersLinkCrypterContainers: allHostersLinkCrypterContainers,
            Images: ToImages(serviceLinks.ImageLinks)
        );
    }

    private UploadCompletionActionMirrorDto ToMirror(
        ReleaseForumPostUploadReadModel upload,
        HashSet<string> allHostersContainerUrls
    )
    {
        var linkCrypterContainers = upload
            .LinkCrypters.Where(linkCrypter =>
                linkCrypter.ContainerUrl != string.Empty
                && !allHostersContainerUrls.Contains(linkCrypter.ContainerUrl)
            )
            .Select(ToLinkCrypterContainer)
            .ToList();

        var allFileSizesAreKnown = upload.FileSizesBytes.All(sizeBytes => sizeBytes is not null);

        return new UploadCompletionActionMirrorDto(
            HosterClassName: upload.HosterClassName,
            HosterName: upload.HosterName,
            Links: upload.Links,
            TotalSizeBytes: allFileSizesAreKnown ? upload.FileSizesBytes.Sum() : null,
            LargestPartSizeBytes: allFileSizesAreKnown ? upload.FileSizesBytes.Max() : null,
            PartCount: upload.FileSizesBytes.Count,
            ArchivePassword: upload.ArchivePassword,
            LinkCrypterContainers: linkCrypterContainers
        );
    }

    private UploadCompletionActionLinkCrypterContainerDto ToLinkCrypterContainer(
        ReleaseForumPostLinkCrypterReadModel linkCrypter
    )
    {
        return new UploadCompletionActionLinkCrypterContainerDto(
            LinkCrypterClassName: linkCrypter.LinkCrypterClassName,
            LinkCrypterName: linkCrypterFactory.Get(linkCrypter.LinkCrypterClassName).Name,
            Url: linkCrypter.ContainerUrl
        );
    }

    private List<UploadCompletionActionImageDto> ToImages(
        IReadOnlyList<ForumPostImageLinkReadModel> imageLinks
    )
    {
        var images = new List<UploadCompletionActionImageDto>();

        foreach (var imageLink in imageLinks)
        {
            var imageHosterName = imageHosterFactory.Get(imageLink.ImageHosterClassName).Name;

            var screenshotUrls = imageLink
                .Screenshots.SelectMany(screenshot => screenshot.Urls)
                .Concat(imageLink.ScreenshotGridUrls)
                .ToList();

            images.AddRange(
                BuildFullSizeImages(
                    imageLink: imageLink,
                    imageHosterName: imageHosterName,
                    urls: imageLink.Urls,
                    isCover: true
                )
            );

            images.AddRange(
                BuildFullSizeImages(
                    imageLink: imageLink,
                    imageHosterName: imageHosterName,
                    urls: screenshotUrls,
                    isCover: false
                )
            );
        }

        return images;
    }

    private static List<UploadCompletionActionImageDto> BuildFullSizeImages(
        ForumPostImageLinkReadModel imageLink,
        string imageHosterName,
        IReadOnlyList<ForumPostImageLinkUrlReadModel> urls,
        bool isCover
    )
    {
        return urls.Where(url => url.Size == ImageSize.Full)
            .Select(url => new UploadCompletionActionImageDto(
                ImageHosterClassName: imageLink.ImageHosterClassName,
                ImageHosterName: imageHosterName,
                Url: url.Url,
                IsCover: isCover
            ))
            .ToList();
    }
}
