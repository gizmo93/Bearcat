namespace Bearcat.Domain.Entities;

public class RemoteSourceRegistration : IEntityWithEncryptedSecrets
{
    public const int DefaultMaxConnections = 2;

    public int Id { get; set; }

    public required string Name { get; set; }

    public required string SerializedConfig { get; set; }

    public required string SourceClassName { get; set; }

    public bool IsActive { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public int MaxConnections { get; set; } = DefaultMaxConnections;

    public string GetEncryptedSecrets() => SerializedConfig;

    public string GetRegistrationTypeName() => "Remote source";

    public string? GetRegistrationName() => Name;
}
