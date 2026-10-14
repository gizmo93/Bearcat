namespace Bearcat.Domain.Shared;

public static class FileSizes
{
    public static long GetFileSizeBytesOrZeroWhenMissing(string fullFileName)
    {
        var fileInfo = new FileInfo(fullFileName);

        return fileInfo.Exists ? fileInfo.Length : 0;
    }
}
