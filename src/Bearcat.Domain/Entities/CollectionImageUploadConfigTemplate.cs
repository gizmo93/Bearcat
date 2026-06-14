namespace Bearcat.Domain.Entities;

public class CollectionImageUploadConfigTemplate
{
    public int Id { get; set; }

    public int ReleaseTemplateId { get; set; }

    public ReleaseTemplate ReleaseTemplate { get; set; } = null!;

    public int ImageHosterRegistrationId { get; set; }

    public ImageHosterRegistration ImageHosterRegistration { get; set; } = null!;

    public string? Name { get; set; }
}
