using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ImageUpload
{
    public int Id { get; set; }

    public int ImageUploadConfigId { get; set; }

    public ImageUploadConfig ImageUploadConfig { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UploadedAt { get; set; }

    public UploadState UploadState { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public List<ImageUploadUrl> ImageUrls { get; set; } = [];
}
