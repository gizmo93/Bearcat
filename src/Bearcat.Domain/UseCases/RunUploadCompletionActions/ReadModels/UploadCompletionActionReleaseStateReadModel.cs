namespace Bearcat.Domain.UseCases.RunUploadCompletionActions.ReadModels;

public record UploadCompletionActionReleaseStateReadModel(
    int RegistrationId,
    string RegistrationName,
    bool IsReleaseCompletelyUploaded,
    DateTime? ExecutedAt,
    string? ResultText,
    string? ErrorMessage
);
