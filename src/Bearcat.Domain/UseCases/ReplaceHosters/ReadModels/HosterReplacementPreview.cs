namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record HosterReplacementPreview(
    int UploadConfigCount,
    int UploadConfigTemplateCount,
    int UnstartedUploadCount,
    int UploadCountStayingOnSourceHoster,
    int UploadConfigCountWithoutArchiveSource,
    int AccountCountToDeactivate,
    int UploadCountToMarkOffline,
    IReadOnlyList<SkippedUploadConfigReadModel> SkippedUploadConfigs,
    IReadOnlyList<SkippedUploadConfigTemplateReadModel> SkippedUploadConfigTemplates,
    IReadOnlyList<SkippedUploadConfigReadModel> DeletedUploadConfigs,
    IReadOnlyList<SkippedUploadConfigTemplateReadModel> DeletedUploadConfigTemplates,
    IReadOnlyList<SourceAccountInUnselectedPoolReadModel> SourceAccountsInUnselectedPools,
    IReadOnlyList<SourcePoolBecomingUnusedReadModel> SourcePoolsBecomingUnused
);
