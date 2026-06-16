namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

/// <summary>
/// A point-in-time view of how fast a running upload is currently transferring data.
/// </summary>
public sealed record UploadSpeedSnapshot(int UploadId, double BytesPerSecond);
