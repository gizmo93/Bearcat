namespace Bearcat.Domain.UseCases.ManageUploads.Progress;

public sealed record UploadProgressSnapshot(
    int UploadId,
    double BytesPerSecond,
    long UploadedBytes,
    long TotalBytes
)
{
    public double Percentage =>
        TotalBytes <= 0 ? 0 : Math.Round((double)UploadedBytes / TotalBytes * 100, 0);
}
