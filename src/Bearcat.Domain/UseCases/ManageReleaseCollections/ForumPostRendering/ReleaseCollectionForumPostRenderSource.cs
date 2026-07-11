using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

public class ReleaseCollectionForumPostRenderSource(
    IReleaseCollectionForumPostRepository repository,
    ReleaseForumPostUploadBuilder uploadBuilder,
    ForumPostImageLinkBuilder imageLinkBuilder
) : IForumPostRenderSource
{
    public ForumPostTemplateType Type => ForumPostTemplateType.ReleaseCollection;

    public IReadOnlyList<ForumPostTemplateVariableReadModel> GetVariables()
    {
        var variables = ForumPostTemplateVariableCatalog
            .GetVariables(typeof(ForumPostTemplateCollectionRenderModel))
            .ToList();

        variables.AddRange(ForumPostImageLinkBuilder.Variables);

        return variables;
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

        var releases = new List<ForumPostTemplateCollectionReleaseModel>();

        foreach (var release in collection.Releases)
        {
            var uploads = await uploadBuilder.BuildAsync(release.ReleaseId, cancellationToken);
            releases.Add(
                new ForumPostTemplateCollectionReleaseModel
                {
                    Name = release.Name,
                    Uploads = uploads,
                }
            );
        }

        var renderModel = new ForumPostTemplateCollectionRenderModel
        {
            Collection = new ForumPostTemplateCollectionModel
            {
                Name = collection.Name,
                Key = collection.Key,
                ReleaseGroup = collection.ReleaseGroupName,
            },
            Series = ToSeriesModel(collection.Series),
            Releases = releases,
        };

        var scriptObject = new ScriptObject();

        scriptObject.Import(renderModel, ForumPostTemplateVariableCatalog.ShouldExposeMember);
        scriptObject["imagelinks"] = await imageLinkBuilder.BuildForCollectionAsync(
            entityId,
            cancellationToken
        );

        return scriptObject;
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
