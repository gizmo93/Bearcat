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
    string? CollectionUploadSlotName,
    int CollectionUploadSlotUploadConfigCount,
    int? ReleaseCollectionId,
    string? ReleaseCollectionName,
    int? HosterRegistrationId,
    int? HosterAccountPoolId,
    string HosterClassName,
    bool PremiumOnlyDownload,
    bool HasRunningUpload
)
{
    public UploadTarget UploadTarget =>
        UploadTarget.FromIds(HosterRegistrationId, HosterAccountPoolId);
}
