namespace Bearcat.Domain.UseCases.ManageSeriesDatabases.ReadModels;

public record SeriesDatabaseRegistrationReadModel(
    int Id,
    bool IsActive,
    string SeriesDatabaseName,
    string SeriesDatabaseClassName
);
