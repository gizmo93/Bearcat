namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;

public record FailedReleaseFolderExtractionReadModel(
    int ReleaseFolderObservationId,
    string FolderPath,
    string ErrorMessage,
    DateTime FailedAt
);
