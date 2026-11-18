namespace Bearcat.Domain.UseCases.RunUploadCompletionActions.ReadModels;

public record UploadCompletionActionStateReadModel(
    int RegistrationId,
    string RegistrationName,
    bool IsCompletelyUploaded,
    DateTime? ExecutedAt,
    string? ResultText,
    string? ErrorMessage
);
