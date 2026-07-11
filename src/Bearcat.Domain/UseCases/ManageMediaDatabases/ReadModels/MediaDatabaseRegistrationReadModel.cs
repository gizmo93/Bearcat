namespace Bearcat.Domain.UseCases.ManageMediaDatabases.ReadModels;

public record MediaDatabaseRegistrationReadModel(
    int Id,
    bool IsActive,
    string MediaDatabaseName,
    string MediaDatabaseClassName
);
