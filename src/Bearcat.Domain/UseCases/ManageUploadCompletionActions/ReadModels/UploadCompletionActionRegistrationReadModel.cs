namespace Bearcat.Domain.UseCases.ManageUploadCompletionActions.ReadModels;

public record UploadCompletionActionRegistrationReadModel(
    int Id,
    string Name,
    string ActionClassName,
    string ActionName,
    bool IsActive,
    bool RunForInitialUpload,
    bool RunForReupload,
    bool HasUnreadableSecrets
);
