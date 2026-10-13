namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;

public record HosterAccountPoolReadModel(
    int Id,
    string Name,
    string HosterClassName,
    string HosterName,
    long MinimumFreeSpaceBytes,
    IReadOnlyList<HosterAccountPoolMemberReadModel> MembersInPriorityOrder
);
