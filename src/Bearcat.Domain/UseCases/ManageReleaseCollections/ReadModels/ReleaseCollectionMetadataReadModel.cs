namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseCollectionMetadataReadModel(
    string SeriesDatabaseName,
    string Title,
    string? Description,
    string? CoverUrl,
    string? SeriesDatabaseUrl
);
