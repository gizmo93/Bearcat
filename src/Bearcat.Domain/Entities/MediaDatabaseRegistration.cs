namespace Bearcat.Domain.Entities;

public class MediaDatabaseRegistration : IEntityWithEncryptedSecrets
{
    public int Id { get; set; }

    public string MediaDatabaseClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public string GetEncryptedSecrets() => SerializedConfig;

    public string GetRegistrationTypeName() => "Media database";

    public string? GetRegistrationName() => MediaDatabaseClassName;
}
