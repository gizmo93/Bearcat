namespace Bearcat.Domain.Entities;

public class LinkCrypterRegistration : IEntityWithEncryptedSecrets
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string LinkCrypterClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public string GetEncryptedSecrets() => SerializedConfig;

    public string GetRegistrationTypeName() => "Link crypter";

    public string? GetRegistrationName() => Name;
}
