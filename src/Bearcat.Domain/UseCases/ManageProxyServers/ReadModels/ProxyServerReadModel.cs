using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

public record ProxyServerReadModel(
    int Id,
    string Name,
    ProxyType ProxyType,
    string Host,
    int Port,
    string? Username,
    bool HasStoredPassword,
    bool HasUnreadableSecrets
);
