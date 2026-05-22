namespace Bearcat.Domain.UseCases.ManageNfoDatabases.ReadModels;

public record NfoDatabaseRegistrationReadModel(
    int Id,
    bool IsActive,
    string NfoDatabaseName,
    string NfoDatabaseClassName,
    IReadOnlyDictionary<string, string> Configuration
);
