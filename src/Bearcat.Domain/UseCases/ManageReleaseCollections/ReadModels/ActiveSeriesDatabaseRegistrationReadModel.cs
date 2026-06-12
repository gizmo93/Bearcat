namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record ActiveSeriesDatabaseRegistrationReadModel(
    string SeriesDatabaseClassName,
    string SerializedConfig
);
