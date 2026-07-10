namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

public record CollectionForumPostReadModel(
    string Name,
    string Key,
    string ReleaseGroupName,
    CollectionForumPostSeriesReadModel? Series,
    IReadOnlyList<CollectionForumPostReleaseReadModel> Releases
);

public record CollectionForumPostSeriesReadModel(
    string Title,
    string? Description,
    string? CoverUrl,
    string? MetadataDatabaseUrl
);

public record CollectionForumPostReleaseReadModel(int ReleaseId, string Name);
