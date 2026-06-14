namespace Bearcat.Domain.Entities;

public class ImageUploadConfig
{
    public int Id { get; set; }

    public int? ReleaseId { get; set; }

    public Release? Release { get; set; }

    public int? ReleaseCollectionId { get; set; }

    public ReleaseCollection? ReleaseCollection { get; set; }

    public int ImageHosterRegistrationId { get; set; }

    public ImageHosterRegistration ImageHosterRegistration { get; set; } = null!;

    public string Name { get; set; } = null!;

    public List<ImageUpload> ImageUploads { get; set; } = [];
}
