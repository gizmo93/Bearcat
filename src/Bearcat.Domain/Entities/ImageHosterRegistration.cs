using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Shared.Entities;

namespace Bearcat.Domain.Entities;

public class ImageHosterRegistration : IEntityWithEncryptedSecrets, IActivatableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string ImageHosterClassName { get; set; } = null!;

    public string SerializedConfig { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool HasUnreadableSecrets { get; set; }

    public ProxySelection ProxySelection { get; set; } = ProxySelection.UseCategoryDefault;

    public int? ProxyServerId { get; set; }

    public List<ImageUploadConfig> ImageUploadConfigs { get; set; } = [];

    public List<ImageUploadConfigTemplate> ImageUploadConfigTemplates { get; set; } = [];

    public List<CollectionImageUploadConfigTemplate> CollectionImageUploadConfigTemplates { get; set; } =
    [];

    public string GetEncryptedSecrets() => SerializedConfig;

    public string GetRegistrationTypeName() => "Image hoster";

    public string? GetRegistrationName() => Name;
}
