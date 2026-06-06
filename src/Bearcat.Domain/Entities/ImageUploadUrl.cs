using Bearcat.Abstractions.ImageHoster.Results;

namespace Bearcat.Domain.Entities;

public class ImageUploadUrl
{
    public int Id { get; set; }

    public int ImageUploadId { get; set; }

    public ImageUpload ImageUpload { get; set; } = null!;

    public ImageSize ImageSize { get; set; }

    public string Url { get; set; } = null!;
}
