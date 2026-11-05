using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;

namespace Bearcat.Domain.UseCases.ReplaceHosters.Dto;

public sealed record HosterReplacementChanges(
    UploadTarget ReplacementUploadTarget,
    string ReplacementUploadTargetName,
    bool ReplacementHosterSupportsPremiumOnlyDownloads,
    IReadOnlyList<int> ReplacedUploadConfigIds,
    IReadOnlyList<int> RenamedUploadConfigIds,
    IReadOnlyList<int> UploadConfigIdsToResetPremiumOnlyDownload,
    IReadOnlyList<int> ReplacedUploadConfigTemplateIds,
    IReadOnlyList<int> RenamedUploadConfigTemplateIds,
    IReadOnlyList<int> UploadConfigTemplateIdsToResetPremiumOnlyDownload
);
