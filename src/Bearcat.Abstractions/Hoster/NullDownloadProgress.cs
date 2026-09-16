namespace Bearcat.Abstractions.Hoster;

public sealed class NullDownloadProgress : IDownloadProgress
{
    public static NullDownloadProgress Instance { get; } = new();

    private NullDownloadProgress() { }

    public void BeginFile(long? totalBytes) { }

    public void ReportBytesTransferred(long bytes) { }
}
