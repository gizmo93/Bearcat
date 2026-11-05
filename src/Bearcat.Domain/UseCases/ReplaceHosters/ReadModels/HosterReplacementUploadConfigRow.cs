using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record HosterReplacementUploadConfigRow(
    int Id,
    int ReleaseId,
    string ReleaseName,
    string Name,
    UploadSource UploadSource,
    int? ArchiveConfigId,
    int? CollectionUploadSlotId,
    int? HosterRegistrationId,
    int? HosterAccountPoolId,
    string HosterClassName,
    bool PremiumOnlyDownload
)
{
    public UploadTarget UploadTarget =>
        UploadTarget.FromIds(HosterRegistrationId, HosterAccountPoolId);
}
