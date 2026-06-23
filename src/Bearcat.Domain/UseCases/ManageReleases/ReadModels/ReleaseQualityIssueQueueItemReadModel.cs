namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseQualityIssueQueueItemReadModel(
    int ReleaseId,
    string ReleaseName,
    string ReleaseGroupName,
    DateTime? EvaluatedAt,
    IReadOnlyList<string> Issues
);
