using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ImageUpload
{
    public int Id { get; set; }

    public int ImageUploadConfigId { get; set; }

    public ImageUploadConfig ImageUploadConfig { get; set; } = null!;

    public int? ReleaseScreenshotId { get; set; }

    public ReleaseScreenshot? ReleaseScreenshot { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UploadedAt { get; set; }

    public UploadState UploadState { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public List<ImageUploadUrl> ImageUrls { get; set; } = [];

    public OnlineState OnlineState { get; set; } = OnlineState.Unknown;

    public DateTime? OnlineCheckedAt { get; set; }

    public DateTime? OfflineSince { get; set; }

    public void SetOnlineStateAndCheckedAt(OnlineState onlineState, DateTime checkedAt)
    {
        OnlineState = onlineState;
        OnlineCheckedAt = checkedAt;
        OfflineSince = onlineState == OnlineState.Offline ? OfflineSince ?? checkedAt : null;
    }
}
