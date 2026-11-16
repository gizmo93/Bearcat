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

    public static string? GetArchiveBaseName(string fileName)
    {
        Regex[] patterns =
        [
            RarPartWithPartNumberPattern(),
            RarPartPattern(),
            SevenZipPartPattern(),
        ];

        var match = patterns
            .Select(pattern => pattern.Match(fileName))
            .FirstOrDefault(match => match.Success);

        return match?.Groups["baseName"].Value;
    }

    [GeneratedRegex(@"^(?<baseName>.+)\.part\d+\.rar$", RegexOptions.IgnoreCase)]
    private static partial Regex RarPartWithPartNumberPattern();

    [GeneratedRegex(@"^(?<baseName>.+)\.(rar|r\d{2,3})$", RegexOptions.IgnoreCase)]
    private static partial Regex RarPartPattern();

    [GeneratedRegex(@"^(?<baseName>.+)\.7z(\.\d{3})?$", RegexOptions.IgnoreCase)]
    private static partial Regex SevenZipPartPattern();
}
