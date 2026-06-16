namespace Bearcat.Abstractions.Hoster;

public interface IUploadProgress
{
    /// <summary>
    /// Reports an additional chunk of bytes that has just been sent for the current file.
    /// </summary>
    /// <param name="bytes">The number of bytes transferred since the previous call.</param>
    void ReportBytesTransferred(long bytes);
}
