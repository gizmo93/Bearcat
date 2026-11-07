using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;

namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record HosterReplacementSourceChoiceReadModel(
    UploadTarget UploadTarget,
    string Name,
    bool IsActive,
    int UploadConfigCount,
    int UploadConfigTemplateCount
)
{
    public bool IsHosterAccountPool => UploadTarget.HosterAccountPoolId is not null;
}
