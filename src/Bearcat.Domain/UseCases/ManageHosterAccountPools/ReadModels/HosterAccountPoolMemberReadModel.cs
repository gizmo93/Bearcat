namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;

public record HosterAccountPoolMemberReadModel(
    int HosterRegistrationId,
    string Name,
    bool IsActive,
    bool HasUnreadableSecrets,
    int Priority
);
