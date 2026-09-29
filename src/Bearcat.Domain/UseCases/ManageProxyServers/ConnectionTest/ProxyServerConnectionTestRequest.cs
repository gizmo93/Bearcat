using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;

public record ProxyServerConnectionTestRequest(
    ProxyType ProxyType,
    string Host,
    int Port,
    string? Username,
    string? Password
);
