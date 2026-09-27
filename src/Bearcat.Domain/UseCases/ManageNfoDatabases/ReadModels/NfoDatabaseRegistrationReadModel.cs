namespace Bearcat.Domain.UseCases.ManageNfoDatabases.ReadModels;

public record NfoDatabaseRegistrationReadModel(
    int Id,
    bool IsActive,
    bool HasUnreadableSecrets,
    string NfoDatabaseName,
    string NfoDatabaseClassName
);
