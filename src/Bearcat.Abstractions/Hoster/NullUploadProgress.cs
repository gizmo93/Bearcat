namespace Bearcat.Abstractions.Hoster;

public sealed class NullUploadProgress : IUploadProgress
{
    public static NullUploadProgress Instance { get; } = new();

    private NullUploadProgress() { }

    public void ReportBytesTransferred(long bytes) { }
}
