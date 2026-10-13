namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;

public record HosterAccountPoolCandidateReadModel(
    int HosterRegistrationId,
    string Name,
    bool IsActive,
    bool HasUnreadableSecrets
);
