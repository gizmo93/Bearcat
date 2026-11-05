namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record SkippedUploadConfigReadModel(
    int UploadConfigId,
    int ReleaseId,
    string ReleaseName,
    string UploadConfigName,
    HosterReplacementSkipReason SkipReason
);
