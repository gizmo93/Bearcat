namespace Bearcat.Domain.UseCases.ManageHosters.ReadModels;

public record HosterRegistrationReadModel(
    int Id,
    string Name,
    bool IsActive,
    string HosterName,
    string FullClassName,
    IReadOnlyDictionary<string, string> Configuration
);
