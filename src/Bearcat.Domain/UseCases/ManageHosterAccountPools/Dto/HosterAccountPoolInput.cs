namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.Dto;

public record HosterAccountPoolInput(
    string Name,
    long MinimumFreeSpaceBytes,
    IReadOnlyList<int> MemberHosterRegistrationIdsInPriorityOrder
);
