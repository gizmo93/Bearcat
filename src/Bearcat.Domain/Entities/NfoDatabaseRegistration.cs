using Bearcat.Domain.Shared.Entities;

namespace Bearcat.Domain.Entities;

public class NfoDatabaseRegistration : IEntityWithEncryptedSecrets, IActivatableEntity
{
    public int Id { get; set; }

    public string NfoDatabaseClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public string GetEncryptedSecrets() => SerializedConfig;

    public string GetRegistrationTypeName() => "NFO database";

    public string? GetRegistrationName() => NfoDatabaseClassName;
}
