namespace Bearcat.Domain.UseCases.ManageMediaDatabases.ReadModels;

public record MediaDatabaseRegistrationReadModel(
    int Id,
    bool IsActive,
    bool HasUnreadableSecrets,
    string MediaDatabaseName,
    string MediaDatabaseClassName
);
