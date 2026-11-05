namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record HosterReplacementPreview(
    int UploadConfigCount,
    int UploadConfigTemplateCount,
    int UnstartedUploadCount,
    int UploadCountStayingOnSourceHoster,
    int UploadConfigCountWithoutArchiveSource,
    IReadOnlyList<SkippedUploadConfigReadModel> SkippedUploadConfigs,
    IReadOnlyList<SkippedUploadConfigTemplateReadModel> SkippedUploadConfigTemplates,
    IReadOnlyList<SourceAccountInUnselectedPoolReadModel> SourceAccountsInUnselectedPools,
    IReadOnlyList<SourcePoolBecomingUnusedReadModel> SourcePoolsBecomingUnused
);
