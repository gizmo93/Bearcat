namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;

public record CollectionReleaseArchiveConfigTarget(
    int ReleaseId,
    int ArchiveConfigId,
    string? ArchiveConfigName = null
);
