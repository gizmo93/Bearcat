using Bearcat.Domain.Shared.ForumPostRendering;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;

public record CollectionForumPostUploadSlotReadModel(
    int Id,
    string Name,
    string Key,
    IReadOnlyList<ReleaseForumPostLinkCrypterReadModel> LinkCrypters
);
