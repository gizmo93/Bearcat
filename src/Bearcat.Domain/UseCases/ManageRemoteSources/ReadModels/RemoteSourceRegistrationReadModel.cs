namespace Bearcat.Domain.UseCases.ManageRemoteSources.ReadModels;

public record RemoteSourceRegistrationReadModel(
    int Id,
    string Name,
    string SourceClassName,
    string SourceName,
    bool IsActive,
    int MaxConnections
);
