namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseCollectionMetadataReadModel(
    string MetadataDatabaseName,
    string Title,
    string? Description,
    string? CoverUrl,
    string? MetadataDatabaseUrl
);
