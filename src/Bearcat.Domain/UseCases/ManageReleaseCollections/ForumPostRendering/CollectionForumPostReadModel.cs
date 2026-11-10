namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

public record CollectionForumPostReadModel(
    string Name,
    string Key,
    string ReleaseGroupName,
    string? PrimaryLanguageCode,
    CollectionForumPostSeriesReadModel? Series,
    IReadOnlyList<int> ReleaseIds
);

public record CollectionForumPostSeriesReadModel(
    string Title,
    string? Description,
    string? CoverUrl,
    string? MetadataDatabaseUrl
);
