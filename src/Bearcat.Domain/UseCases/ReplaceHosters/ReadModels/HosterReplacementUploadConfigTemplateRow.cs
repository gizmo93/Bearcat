using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record HosterReplacementUploadConfigTemplateRow(
    int Id,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    string? Name,
    UploadSource UploadSource,
    int? ArchiveConfigTemplateId,
    int? HosterRegistrationId,
    int? HosterAccountPoolId,
    string HosterClassName,
    bool PremiumOnlyDownload
)
{
    public UploadTarget UploadTarget =>
        UploadTarget.FromIds(HosterRegistrationId, HosterAccountPoolId);
}
