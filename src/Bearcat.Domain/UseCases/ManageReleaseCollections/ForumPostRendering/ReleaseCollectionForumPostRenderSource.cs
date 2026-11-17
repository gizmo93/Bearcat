using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

public class ReleaseCollectionForumPostRenderSource(
    IReleaseCollectionForumPostRepository repository,
    ReleaseForumPostRenderModelBuilder releaseRenderModelBuilder,
    IReleaseReadRepository releaseReadRepository,
    IForumPostImageLinkRepository imageLinkRepository,
    IDistributionSiteRegistrationReadRepository distributionSiteRegistrationReadRepository
) : IForumPostRenderSource
{
    private const string CollectionVariableName = "collection";

    private const string SeriesVariableName = "series";

    private const string ReleasesVariablePath = "releases";

    private const string UploadSlotsVariableName = "upload_slots";

    private const string ReleaseImageLinksVariablePath = "release.imagelinks";

    private const string ImageLinksVariableName = "imagelinks";

    public ForumPostTemplateType Type => ForumPostTemplateType.ReleaseCollection;

    public IReadOnlyList<ForumPostTemplateVariableNode> GetVariables()
    {
        var variables = ForumPostTemplateVariableCatalog.GetVariables(
            typeof(ForumPostTemplateCollectionRenderModel)
        );

        return
        [
            .. variables.Select(AddReleaseImageLinksVariableNode),
            ForumPostImageLinkBuilder.CreateVariableNode(ImageLinksVariableName),
        ];
    }

    public async Task<ForumPostRenderGlobals?> BuildGlobalsAsync(
        int entityId,
        int? distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await repository.GetAsync(entityId, cancellationToken);

        if (collection is null)
        {
            return null;
        }

        var serviceLinksByReleaseId = await releaseRenderModelBuilder.LoadServiceLinksAsync(
            collection.ReleaseIds,
            cancellationToken
        );

        var collectionImageLinks = await imageLinkRepository.GetCollectionImageLinksAsync(
            entityId,
            cancellationToken
        );

        var restrictions = distributionSiteRegistrationId is null
            ? DistributionSiteServiceRestrictions.Empty
            : await distributionSiteRegistrationReadRepository.GetServiceRestrictionsAsync(
                distributionSiteRegistrationId.Value,
                cancellationToken
            );

        var memberServiceLinks = serviceLinksByReleaseId.Values.ToList();

        var memberUploads = memberServiceLinks
            .SelectMany(serviceLinks => serviceLinks.Uploads)
            .ToList();

        var multiHosterLinkCrypters = memberServiceLinks
            .SelectMany(serviceLinks => serviceLinks.AllHostersLinkCrypters)
            .Concat(collection.UploadSlots.SelectMany(uploadSlot => uploadSlot.LinkCrypters))
            .ToList();

        var imageLinks = memberServiceLinks
            .SelectMany(serviceLinks => serviceLinks.ImageLinks)
            .Concat(collectionImageLinks)
            .ToList();

        var blockedReasons = restrictions.FindBlockedReasons(
            uploads: memberUploads,
            multiHosterLinkCrypters: multiHosterLinkCrypters,
            imageLinks: imageLinks
        );

        var renderDataByReleaseId = await releaseRenderModelBuilder.BuildAsync(
            serviceLinksByReleaseId.ToDictionary(
                pair => pair.Key,
                pair => restrictions.RemoveForbiddenServiceLinks(pair.Value)
            ),
            cancellationToken
        );

        var externalIdentifiersByReleaseId =
            await releaseReadRepository.GetReleaseExternalIdentifiersByReleaseIdAsync(
                collection.ReleaseIds,
                cancellationToken
            );

        var releaseExternalIdentifiers = externalIdentifiersByReleaseId
            .Values.SelectMany(identifiers => identifiers)
            .ToList();

        var imdbId = FindIdentifierValue(releaseExternalIdentifiers, ExternalIdentifierType.Imdb);
        var tmdbId = FindIdentifierValue(releaseExternalIdentifiers, ExternalIdentifierType.Tmdb);
        var tvdbId = FindIdentifierValue(releaseExternalIdentifiers, ExternalIdentifierType.Tvdb);

        var releaseScriptObjects = new ScriptArray();

        foreach (var releaseId in collection.ReleaseIds)
        {
            var renderData = renderDataByReleaseId[releaseId];
            var releaseScriptObject = new ScriptObject();

            releaseScriptObject.Import(
                ToCollectionReleaseModel(renderData.RenderModel),
                ForumPostTemplateVariableCatalog.ShouldExposeMember
            );

            releaseScriptObject[ImageLinksVariableName] = renderData.ImageLinks;
            releaseScriptObjects.Add(releaseScriptObject);
        }

        var scriptObject = new ScriptObject
        {
            [CollectionVariableName] = new ForumPostTemplateCollectionModel
            {
                Name = collection.Name,
                Key = collection.Key,
                ReleaseGroup = collection.ReleaseGroupName,
                PrimaryLanguage = ReleaseForumPostRenderModelBuilder.GetLanguageName(
                    collection.PrimaryLanguageCode
                ),
                ImdbId = imdbId,
                ImdbUrl =
                    imdbId.Length == 0 ? string.Empty : $"https://www.imdb.com/title/{imdbId}",
                TmdbId = tmdbId.Length == 0 ? string.Empty : tmdbId.Split('/')[1],
                TmdbUrl =
                    tmdbId.Length == 0 ? string.Empty : $"https://www.themoviedb.org/{tmdbId}",
                TvdbId = tvdbId,
                TvdbUrl =
                    tvdbId.Length == 0
                        ? string.Empty
                        : $"https://www.thetvdb.com/dereferrer/series/{tvdbId}",
                MainVideoTotalSizeBytes = renderDataByReleaseId.Values.Sum(renderData =>
                    renderData.RenderModel.Release.MainVideo.SizeBytes
                ),
            },
            [SeriesVariableName] = ToSeriesModel(collection.Series),
            [ReleasesVariablePath] = releaseScriptObjects,
            [UploadSlotsVariableName] = collection
                .UploadSlots.Select(uploadSlot => ToUploadSlotModel(uploadSlot, restrictions))
                .ToList(),
        };

        scriptObject[ImageLinksVariableName] = ForumPostImageLinkBuilder.ToCollectionScriptObject(
            restrictions.RemoveForbiddenImageLinks(collectionImageLinks)
        );

        return new ForumPostRenderGlobals(scriptObject, blockedReasons);
    }

    private static string FindIdentifierValue(
        IReadOnlyList<ReleaseExternalIdentifierReadModel> externalIdentifiers,
        ExternalIdentifierType type
    )
    {
        var identifier = ReleaseExternalIdentifierService.FindIdentifierWithHighestSourcePriority(
            externalIdentifiers,
            type
        );

        return identifier?.Value ?? string.Empty;
    }

    private static ForumPostTemplateVariableNode AddReleaseImageLinksVariableNode(
        ForumPostTemplateVariableNode variable
    )
    {
        if (variable.Path != ReleasesVariablePath)
        {
            return variable;
        }

        return variable with
        {
            Children =
            [
                .. variable.Children,
                ForumPostImageLinkBuilder.CreateReleaseVariableNode(ReleaseImageLinksVariablePath),
            ],
        };
    }

    private static ForumPostTemplateCollectionReleaseModel ToCollectionReleaseModel(
        ForumPostTemplateRenderModel renderModel
    )
    {
        var release = renderModel.Release;

        return new ForumPostTemplateCollectionReleaseModel
        {
            Name = release.Name,
            PrimaryLanguage = release.PrimaryLanguage,
            Nfo = release.Nfo,
            SteamAppId = release.SteamAppId,
            SteamUrl = release.SteamUrl,
            ImdbId = release.ImdbId,
            ImdbUrl = release.ImdbUrl,
            TmdbId = release.TmdbId,
            TmdbUrl = release.TmdbUrl,
            MainVideo = release.MainVideo,
            MediaFiles = release.MediaFiles,
            ReleaseInfo = renderModel.ReleaseInfo,
            Classification = renderModel.Classification,
            Uploads = renderModel.Uploads,
            LinkCrypters = renderModel.LinkCrypters,
        };
    }

    private static ForumPostTemplateCollectionUploadSlotModel ToUploadSlotModel(
        CollectionForumPostUploadSlotReadModel uploadSlot,
        DistributionSiteServiceRestrictions restrictions
    )
    {
        return new ForumPostTemplateCollectionUploadSlotModel
        {
            Name = uploadSlot.Name,
            Key = uploadSlot.Key,
            LinkCrypters = restrictions
                .RemoveForbiddenLinkCrypters(uploadSlot.LinkCrypters)
                .Select(ReleaseForumPostUploadBuilder.ToLinkCrypterModel)
                .ToList(),
        };
    }

    private static ForumPostTemplateSeriesModel ToSeriesModel(
        CollectionForumPostSeriesReadModel? series
    )
    {
        if (series is null)
        {
            return ForumPostTemplateSeriesModel.Empty;
        }

        return new ForumPostTemplateSeriesModel
        {
            Title = series.Title,
            Description = series.Description ?? string.Empty,
            CoverUrl = series.CoverUrl ?? string.Empty,
            DatabaseUrl = series.MetadataDatabaseUrl ?? string.Empty,
        };
    }
}
