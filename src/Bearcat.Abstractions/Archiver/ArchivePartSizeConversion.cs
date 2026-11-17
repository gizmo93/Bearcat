namespace Bearcat.Abstractions.Archiver;

public static class ArchivePartSizeConversion
{
    private const long BytesPerMegabyte = 1_000_000;
    private const long BytesPerMebibyte = 1_048_576;

    public static int ConvertMegabytesToPartSizeUnit(
        int megabytes,
        ArchivePartSizeUnit partSizeUnit
    )
    {
        return (int)(megabytes * BytesPerMegabyte / GetBytesPerPartSizeUnit(partSizeUnit));
    }

    public static long ConvertPartSizeToBytes(int partSize, ArchivePartSizeUnit partSizeUnit)
    {
        return partSize * GetBytesPerPartSizeUnit(partSizeUnit);
    }

    private static long GetBytesPerPartSizeUnit(ArchivePartSizeUnit partSizeUnit)
    {
        return partSizeUnit switch
        {
            ArchivePartSizeUnit.Megabytes => BytesPerMegabyte,
            ArchivePartSizeUnit.Mebibytes => BytesPerMebibyte,
            _ => throw new ArgumentOutOfRangeException(nameof(partSizeUnit), partSizeUnit, null),
        };
    }
}
