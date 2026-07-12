namespace Bearcat.Abstractions;

public static class SequentialFileReader
{
    private const int BufferSize = 1024 * 1024;

    public static FileStream OpenRead(string fullFileName)
    {
        return new FileStream(
            path: fullFileName,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: BufferSize,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan
        );
    }
}
