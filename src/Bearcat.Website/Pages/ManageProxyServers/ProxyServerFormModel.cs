using Bearcat.Domain.UseCases.ManageProxyServers.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageProxyServers;

public class ProxyServerFormModel
{
    public int? ProxyServerId { get; set; }

    public string? Name { get; set; }

    public ProxyType ProxyType { get; set; } = ProxyType.Http;

    public string? Host { get; set; }

    public int Port { get; set; } = 8080;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool HasStoredPassword { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public bool RemoveStoredPassword { get; set; }

    public ProxyServerInput ToInput()
    {
        return new ProxyServerInput(
            Name ?? string.Empty,
            ProxyType,
            Host ?? string.Empty,
            Port,
            Username,
            Password,
            RemoveStoredPassword
        );
    }
}
