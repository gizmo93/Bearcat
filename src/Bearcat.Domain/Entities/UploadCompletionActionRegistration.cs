using Bearcat.Domain.Shared.Entities;

namespace Bearcat.Domain.Entities;

public class UploadCompletionActionRegistration : IEntityWithEncryptedSecrets, IActivatableEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string ActionClassName { get; set; }

    public required string SerializedConfig { get; set; }

    public bool IsActive { get; set; }

    public bool RunForInitialUpload { get; set; }

    public bool RunForReupload { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public DateTime CreatedAt { get; set; }

    public string GetEncryptedSecrets() => SerializedConfig;

    public string GetRegistrationTypeName() => "Upload completion action";

    public string? GetRegistrationName() => Name;
}
