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
        var bytesPerPartSizeUnit = partSizeUnit switch
        {
            ArchivePartSizeUnit.Megabytes => BytesPerMegabyte,
            ArchivePartSizeUnit.Mebibytes => BytesPerMebibyte,
            _ => throw new ArgumentOutOfRangeException(nameof(partSizeUnit), partSizeUnit, null),
        };

        return (int)(megabytes * BytesPerMegabyte / bytesPerPartSizeUnit);
    }
}
