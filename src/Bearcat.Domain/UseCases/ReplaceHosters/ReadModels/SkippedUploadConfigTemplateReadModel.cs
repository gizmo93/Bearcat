namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record SkippedUploadConfigTemplateReadModel(
    int UploadConfigTemplateId,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    string UploadConfigTemplateDisplayName,
    HosterReplacementSkipReason SkipReason
);
