using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

public record CollectionForumPostReadModel(
    string Name,
    string Key,
    ReleaseContentType ReleaseContentType,
    string ReleaseGroupName,
    string? PrimaryLanguageCode,
    CollectionForumPostSeriesReadModel? Series,
    IReadOnlyList<int> ReleaseIds,
    IReadOnlyList<CollectionForumPostUploadSlotReadModel> UploadSlots
);

public record CollectionForumPostSeriesReadModel(
    string Title,
    string? Description,
    string? CoverUrl,
    string? MetadataDatabaseUrl
);
