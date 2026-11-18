using System.Globalization;
using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;

namespace Bearcat.Domain.UseCases.RunUploadCompletionActions;

public class UploadCompletionActionInputBuilder(
    ReleaseForumPostRenderModelBuilder releaseForumPostRenderModelBuilder,
    IReleaseCollectionForumPostRepository releaseCollectionForumPostRepository,
    IForumPostImageLinkRepository imageLinkRepository,
    IImageHosterFactory imageHosterFactory,
    ILinkCrypterFactory linkCrypterFactory
)
{
    public async Task<UploadCompletionActionInput> BuildAsync(
        UploadCompletionActionDueRelease dueRelease,
        CancellationToken cancellationToken
    )
    {
        var serviceLinksByReleaseId =
            await releaseForumPostRenderModelBuilder.LoadServiceLinksAsync(
                [dueRelease.ReleaseId],
                cancellationToken
            );

        var renderDataByReleaseId = await releaseForumPostRenderModelBuilder.BuildAsync(
            serviceLinksByReleaseId,
            cancellationToken
        );

        var release = ToRelease(renderDataByReleaseId[dueRelease.ReleaseId].RenderModel);
        var serviceLinks = serviceLinksByReleaseId[dueRelease.ReleaseId];

        var allHostersLinkCrypterContainers = serviceLinks
            .AllHostersLinkCrypters.Select(ToLinkCrypterContainer)
            .ToList();

        var allHostersContainerUrls = allHostersLinkCrypterContainers
            .Select(container => container.Url)
            .ToHashSet();

        var mirrors = serviceLinks
            .Uploads.Where(upload =>
                upload.CollectionUploadSlotId is null && upload.Links.Count > 0
            )
            .Select(upload => ToMirror(upload, allHostersContainerUrls))
            .ToList();

        return new UploadCompletionActionInput(
            Release: release,
            CollectionReleaseNames: [],
            IsReupload: dueRelease.IsReupload,
            Mirrors: mirrors,
            AllHostersLinkCrypterContainers: allHostersLinkCrypterContainers,
            Images: ToImages(serviceLinks.ImageLinks)
        );
    }

    public async Task<UploadCompletionActionInput> BuildAsync(
        UploadCompletionActionDueReleaseCollection dueReleaseCollection,
        CancellationToken cancellationToken
    )
    {
        var collection = (
            await releaseCollectionForumPostRepository.GetAsync(
                dueReleaseCollection.ReleaseCollectionId,
                cancellationToken
            )
        )!;

        var serviceLinksByReleaseId =
            await releaseForumPostRenderModelBuilder.LoadServiceLinksAsync(
                collection.ReleaseIds,
                cancellationToken
            );

        var renderDataByReleaseId = await releaseForumPostRenderModelBuilder.BuildAsync(
            serviceLinksByReleaseId,
            cancellationToken
        );

        var collectionImageLinks = await imageLinkRepository.GetCollectionImageLinksAsync(
            dueReleaseCollection.ReleaseCollectionId,
            cancellationToken
        );

        var memberReleases = collection
            .ReleaseIds.Select(releaseId => ToRelease(renderDataByReleaseId[releaseId].RenderModel))
            .ToList();

        var slotUploads = collection
            .ReleaseIds.SelectMany(releaseId => serviceLinksByReleaseId[releaseId].Uploads)
            .Where(upload => upload.CollectionUploadSlotId is not null && upload.Links.Count > 0)
            .ToList();

        var mirrors = collection
            .UploadSlots.SelectMany(uploadSlot => BuildUploadSlotMirrors(uploadSlot, slotUploads))
            .ToList();

        return new UploadCompletionActionInput(
            Release: ToCollectionRelease(collection, memberReleases),
            CollectionReleaseNames: memberReleases.Select(release => release.Name).ToList(),
            IsReupload: dueReleaseCollection.IsReupload,
            Mirrors: mirrors,
            AllHostersLinkCrypterContainers: [],
            Images: ToImages(collectionImageLinks)
        );
    }

    private static UploadCompletionActionReleaseDto ToCollectionRelease(
        CollectionForumPostReadModel collection,
        List<UploadCompletionActionReleaseDto> memberReleases
    )
    {
        var audioLanguages = memberReleases
            .First()
            .AudioLanguages.Where(language =>
                memberReleases.All(release =>
                    release.AudioLanguages.Contains(language, StringComparer.OrdinalIgnoreCase)
                )
            )
            .ToList();

        return new UploadCompletionActionReleaseDto(
            Name: collection.Name,
            ContentType: collection.ReleaseContentType.ToString(),
            Platform: GetCommonValueOrDefault(memberReleases, release => release.Platform),
            Title: collection.Series?.Title.TrimOrNullWhenWhitespace(),
            Genre: GetCommonValueOrDefault(memberReleases, release => release.Genre),
            Description: collection.Series?.Description.TrimOrNullWhenWhitespace(),
            RuntimeMinutes: null,
            VideoType: GetCommonValueOrDefault(memberReleases, release => release.VideoType),
            VideoCodec: GetCommonValueOrDefault(memberReleases, release => release.VideoCodec),
            AudioType: GetCommonValueOrDefault(memberReleases, release => release.AudioType),
            AudioCodec: GetCommonValueOrDefault(memberReleases, release => release.AudioCodec),
            AudioChannels: GetCommonValueOrDefault(
                memberReleases,
                release => release.AudioChannels
            ),
            AudioLanguages: audioLanguages,
            Nfo: null
        );
    }

    private static T? GetCommonValueOrDefault<T>(
        List<UploadCompletionActionReleaseDto> releases,
        Func<UploadCompletionActionReleaseDto, T> selector
    )
    {
        var distinctValues = releases.Select(selector).Distinct().ToList();

        return distinctValues.Count == 1 ? distinctValues.Single() : default;
    }

    private List<UploadCompletionActionMirrorDto> BuildUploadSlotMirrors(
        CollectionForumPostUploadSlotReadModel uploadSlot,
        List<ReleaseForumPostUploadReadModel> slotUploads
    )
    {
        var linkCrypterContainers = uploadSlot.LinkCrypters.Select(ToLinkCrypterContainer).ToList();

        return slotUploads
            .Where(upload => upload.CollectionUploadSlotId == uploadSlot.Id)
            .GroupBy(upload => upload.HosterClassName)
            .OrderBy(hosterUploads => hosterUploads.First().HosterName)
            .Select(hosterUploads =>
                ToCollectionMirror(hosterUploads.ToList(), linkCrypterContainers)
            )
            .ToList();
    }

    private static UploadCompletionActionMirrorDto ToCollectionMirror(
        List<ReleaseForumPostUploadReadModel> hosterUploads,
        List<UploadCompletionActionLinkCrypterContainerDto> linkCrypterContainers
    )
    {
        var fileSizesBytes = hosterUploads.SelectMany(upload => upload.FileSizesBytes).ToList();
        var allFileSizesAreKnown = fileSizesBytes.All(sizeBytes => sizeBytes is not null);

        var archivePasswords = hosterUploads
            .Select(upload => upload.ArchivePassword)
            .Distinct()
            .ToList();

        return new UploadCompletionActionMirrorDto(
            HosterClassName: hosterUploads.First().HosterClassName,
            HosterName: hosterUploads.First().HosterName,
            Links: hosterUploads.SelectMany(upload => upload.Links).ToList(),
            TotalSizeBytes: allFileSizesAreKnown ? fileSizesBytes.Sum() : null,
            LargestPartSizeBytes: fileSizesBytes.Max(),
            PartCount: fileSizesBytes.Count,
            ArchivePassword: archivePasswords.Count == 1 ? archivePasswords.Single() : null,
            LinkCrypterContainers: linkCrypterContainers
        );
    }

    private static UploadCompletionActionReleaseDto ToRelease(
        ForumPostTemplateRenderModel renderModel
    )
    {
        var mainVideo = renderModel.Release.MainVideo;

        var audioLanguages = mainVideo
            .AudioStreams.Select(audioStream => audioStream.Language)
            .Where(language => language.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mainAudioStream = mainVideo
            .AudioStreams.OrderByDescending(audioStream => audioStream.IsDefault)
            .FirstOrDefault();

        return new UploadCompletionActionReleaseDto(
            Name: renderModel.Release.Name,
            ContentType: renderModel.Classification.ContentType.TrimOrNullWhenWhitespace(),
            Platform: renderModel.Classification.Platform.TrimOrNullWhenWhitespace(),
            Title: renderModel.Classification.Title.TrimOrNullWhenWhitespace(),
            Genre: renderModel.ReleaseInfo.Genre.TrimOrNullWhenWhitespace(),
            Description: renderModel.ReleaseInfo.Description.TrimOrNullWhenWhitespace(),
            RuntimeMinutes: ToRuntimeMinutes(mainVideo.Duration),
            VideoType: renderModel.ReleaseInfo.VideoType.TrimOrNullWhenWhitespace(),
            VideoCodec: mainVideo.Video.Codec.TrimOrNullWhenWhitespace(),
            AudioType: renderModel.ReleaseInfo.AudioType.TrimOrNullWhenWhitespace(),
            AudioCodec: mainAudioStream?.Codec.TrimOrNullWhenWhitespace(),
            AudioChannels: mainAudioStream?.Channels,
            AudioLanguages: audioLanguages,
            Nfo: renderModel.Release.Nfo.TrimOrNullWhenWhitespace()
        );
    }

    private static int? ToRuntimeMinutes(string duration)
    {
        if (duration.Length == 0)
        {
            return null;
        }

        var timeSpan = TimeSpan.ParseExact(duration, @"hh\:mm\:ss", CultureInfo.InvariantCulture);

        return (int)Math.Round(timeSpan.TotalMinutes);
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
