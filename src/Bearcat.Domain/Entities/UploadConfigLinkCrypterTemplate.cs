namespace Bearcat.Domain.Entities;

public class UploadConfigLinkCrypterTemplate
{
    public int Id { get; set; }

    public int UploadConfigTemplateId { get; set; }

    public UploadConfigTemplate UploadConfigTemplate { get; set; } = null!;

    public int LinkCrypterRegistrationId { get; set; }

    public LinkCrypterRegistration LinkCrypterRegistration { get; set; } = null!;

    public string? Password { get; set; }
}
