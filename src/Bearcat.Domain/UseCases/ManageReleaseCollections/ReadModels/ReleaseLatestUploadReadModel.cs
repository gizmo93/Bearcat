namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ReleaseLatestUploadReadModel(
    int UploadId,
    string UploadConfigName,
    DateTime CreatedAt,
    DateTime? UploadedAt
);
