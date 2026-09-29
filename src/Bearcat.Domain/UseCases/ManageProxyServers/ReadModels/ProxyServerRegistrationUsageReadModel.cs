namespace Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

public record ProxyServerRegistrationUsageReadModel(
    ProxyUsingRegistrationType RegistrationType,
    string RegistrationName
);
