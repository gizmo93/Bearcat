using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;

public record ProxyServerRoutingReadModel(
    int Id,
    string Name,
    ProxyType ProxyType,
    string Host,
    int Port,
    string? Username,
    string? EncryptedPassword,
    bool HasUnreadableSecrets
);
