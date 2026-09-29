using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageProxyServers.Dto;

public record ProxyServerInput(
    string Name,
    ProxyType ProxyType,
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool RemoveStoredPassword
);
