namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;

public record HosterAccountPoolReadModel(
    int Id,
    string Name,
    string HosterClassName,
    string HosterName,
    bool SupportsPremiumOnlyDownloads,
    bool IsVideoHoster,
    int? MaxFileSizeMb,
    long MinimumFreeSpaceBytes,
    IReadOnlyList<HosterAccountPoolMemberReadModel> MembersInPriorityOrder
);
