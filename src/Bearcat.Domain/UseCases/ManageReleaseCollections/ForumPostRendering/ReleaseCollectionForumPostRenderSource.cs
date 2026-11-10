using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.ValueObjects;
using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

public class ReleaseCollectionForumPostRenderSource(
    IReleaseCollectionForumPostRepository repository,
    ReleaseForumPostRenderModelBuilder releaseRenderModelBuilder,
    ForumPostImageLinkBuilder imageLinkBuilder
) : IForumPostRenderSource
{
    private const string CollectionVariableName = "collection";

    private const string SeriesVariableName = "series";

    private const string ReleasesVariablePath = "releases";

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

    public async Task<ScriptObject?> BuildGlobalsAsync(
        int entityId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await repository.GetAsync(entityId, cancellationToken);

        if (collection is null)
        {
            return null;
        }

        var renderDataByReleaseId = await releaseRenderModelBuilder.BuildAsync(
            collection.ReleaseIds,
            cancellationToken
        );

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
            },
            [SeriesVariableName] = ToSeriesModel(collection.Series),
            [ReleasesVariablePath] = releaseScriptObjects,
        };

        scriptObject[ImageLinksVariableName] = await imageLinkBuilder.BuildForCollectionAsync(
            entityId,
            cancellationToken
        );

        return scriptObject;
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
                ForumPostImageLinkBuilder.CreateVariableNode(ReleaseImageLinksVariablePath),
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
            MainVideo = release.MainVideo,
            MediaFiles = release.MediaFiles,
            ReleaseInfo = renderModel.ReleaseInfo,
            Classification = renderModel.Classification,
            Uploads = renderModel.Uploads,
            LinkCrypters = renderModel.LinkCrypters,
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
