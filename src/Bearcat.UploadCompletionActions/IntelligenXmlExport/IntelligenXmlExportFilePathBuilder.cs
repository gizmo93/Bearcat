namespace Bearcat.UploadCompletionActions.IntelligenXmlExport;

public static class IntelligenXmlExportFilePathBuilder
{
    private const char ReplacementCharacter = '_';

    private static readonly char[] InvalidWindowsFileNameCharacters =
    [
        '<',
        '>',
        ':',
        '"',
        '|',
        '?',
        '*',
    ];

    public static string BuildUnusedFilePath(string exportDirectory, string relativeFilePath)
    {
        var fullExportDirectory = Path.GetFullPath(exportDirectory);

        if (!Directory.Exists(fullExportDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Export directory '{fullExportDirectory}' does not exist"
            );
        }

        var segments = relativeFilePath
            .Split('/', '\\')
            .Select(ReplaceInvalidFileNameCharacters)
            .ToList();

        var filePath = Path.GetFullPath(
            Path.Combine(fullExportDirectory, string.Join(Path.DirectorySeparatorChar, segments))
        );

        var exportDirectoryWithSeparator = Path.EndsInDirectorySeparator(fullExportDirectory)
            ? fullExportDirectory
            : fullExportDirectory + Path.DirectorySeparatorChar;

        if (!filePath.StartsWith(exportDirectoryWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Relative file path '{relativeFilePath}' points outside of the export directory '{fullExportDirectory}'"
            );
        }

        return AppendNumberUntilFileDoesNotExist(filePath);
    }

    private static string ReplaceInvalidFileNameCharacters(string segment)
    {
        var characters = segment
            .Select(character =>
                character < ' ' || InvalidWindowsFileNameCharacters.Contains(character)
                    ? ReplacementCharacter
                    : character
            )
            .ToArray();

        return new string(characters);
    }

    private static string AppendNumberUntilFileDoesNotExist(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var unusedFilePath = filePath;
        var number = 2;

        while (File.Exists(unusedFilePath))
        {
            unusedFilePath = Path.Combine(
                directory,
                $"{fileNameWithoutExtension} ({number}){extension}"
            );

            number++;
        }

        return unusedFilePath;
    }
}
