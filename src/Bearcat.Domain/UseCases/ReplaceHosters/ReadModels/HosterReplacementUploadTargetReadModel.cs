using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;

namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record HosterReplacementUploadTargetReadModel(
    UploadTarget UploadTarget,
    string Name,
    string HosterClassName,
    bool IsUsableForUploads
);
