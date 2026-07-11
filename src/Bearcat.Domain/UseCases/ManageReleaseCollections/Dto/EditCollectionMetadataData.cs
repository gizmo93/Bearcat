namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;

public record EditCollectionMetadataData(
    string? Title,
    string? CoverUrl,
    string? Description,
    string? MetadataDatabaseUrl
);
