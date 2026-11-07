namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record SourceAccountInUnselectedPoolReadModel(
    int HosterAccountPoolId,
    string HosterAccountPoolName,
    int HosterRegistrationId,
    string HosterRegistrationName
);
