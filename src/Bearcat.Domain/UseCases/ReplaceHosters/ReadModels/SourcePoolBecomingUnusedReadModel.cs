namespace Bearcat.Domain.UseCases.ReplaceHosters.ReadModels;

public sealed record SourcePoolBecomingUnusedReadModel(
    int HosterAccountPoolId,
    string HosterAccountPoolName
);
