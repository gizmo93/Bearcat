using System.Text.RegularExpressions;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public static partial class ArchivePartFileNames
{
    public const string RarFileExtension = ".rar";

    public const string SevenZipFileExtension = ".7z";

    public static string? GetArchiverFileExtension(string fileName)
    {
        if (RarPartPattern().IsMatch(fileName))
        {
            return RarFileExtension;
        }

        if (SevenZipPartPattern().IsMatch(fileName))
        {
            return SevenZipFileExtension;
        }

        return null;
    }

    public static bool IsArchivePart(string fileName) =>
        GetArchiverFileExtension(fileName) is not null;

    [GeneratedRegex(@"^.+\.(rar|r\d{2,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex RarPartPattern();

    [GeneratedRegex(@"^.+\.7z(\.\d{3})?$", RegexOptions.IgnoreCase)]
    private static partial Regex SevenZipPartPattern();
}
