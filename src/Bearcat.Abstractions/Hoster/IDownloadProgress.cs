namespace Bearcat.Abstractions.Hoster;

public interface IDownloadProgress
{
    /// <summary>
    /// Signals that a fresh transfer of the current file is starting, discarding any bytes
    /// reported for a previous attempt of the same file.
    /// </summary>
    /// <param name="totalBytes">
    /// The size of the file as announced by the hoster, or <c>null</c> when it is unknown.
    /// </param>
    void BeginFile(long? totalBytes);

    /// <summary>
    /// Reports an additional chunk of bytes that has just been received for the current file.
    /// </summary>
    /// <param name="bytes">The number of bytes transferred since the previous call.</param>
    void ReportBytesTransferred(long bytes);
}
