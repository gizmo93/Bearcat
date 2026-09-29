using Bearcat.Domain.Shared.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ProxyServer : IEntityWithEncryptedSecrets
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public ProxyType ProxyType { get; set; }

    public required string Host { get; set; }

    public int Port { get; set; }

    public string? Username { get; set; }

    public string? EncryptedPassword { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public string GetEncryptedSecrets() => EncryptedPassword!;

    public string GetRegistrationTypeName() => "Proxy server";

    public string? GetRegistrationName() => Name;
}
