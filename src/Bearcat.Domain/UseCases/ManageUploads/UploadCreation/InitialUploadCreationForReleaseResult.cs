namespace Bearcat.Domain.UseCases.ManageUploads.UploadCreation;

public record InitialUploadCreationForReleaseResult(
    int CreatedUploadCount,
    bool IsBlockedByQualityGate
);
