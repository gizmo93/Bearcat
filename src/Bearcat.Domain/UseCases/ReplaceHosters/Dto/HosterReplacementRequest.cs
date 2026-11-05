using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;

namespace Bearcat.Domain.UseCases.ReplaceHosters.Dto;

public sealed record HosterReplacementRequest(
    IReadOnlyList<UploadTarget> SourceUploadTargets,
    UploadTarget ReplacementUploadTarget
);
