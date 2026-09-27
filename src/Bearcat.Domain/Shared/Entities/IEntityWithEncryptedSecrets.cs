namespace Bearcat.Domain.Shared.Entities;

public interface IEntityWithEncryptedSecrets
{
    bool HasUnreadableSecrets { get; set; }

    string GetEncryptedSecrets();

    string GetRegistrationTypeName();

    string? GetRegistrationName();
}
