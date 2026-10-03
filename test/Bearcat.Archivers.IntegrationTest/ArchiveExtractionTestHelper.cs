using Bearcat.Abstractions.Archiver;
using Shouldly;

namespace Bearcat.Archivers.IntegrationTest;

public static class ArchiveExtractionTestHelper
{
    public static async Task<string> WriteRandomSourceFileAsync(string sourceFolderPath)
    {
        var sourceFilePath = Path.Combine(sourceFolderPath, "payload.bin");
        var data = new byte[3 * 1024 * 1024];
        new Random(42).NextBytes(data);

        await File.WriteAllBytesAsync(sourceFilePath, data);

        return sourceFilePath;
    }

    public static void CreateEmptyFiles(string folderPath, List<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            File.WriteAllBytes(Path.Combine(folderPath, fileName), []);
        }
    }

    public static async Task AppendNullByteToEachArchiveFileAsync(ArchiveResult archiveResult)
    {
        foreach (var fileName in archiveResult.CreatedFileNames)
        {
            await AppendNullByteToArchiveFileAsync(fileName);
        }
    }

    public static async Task AppendNullByteToArchiveFileAsync(string fileName)
    {
        await using var stream = new FileStream(
            fileName,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read
        );
        stream.WriteByte(0);
        await stream.FlushAsync();
    }

    public static void ExtractedPayloadShouldMatchSource(string sourceFilePath, string extractPath)
    {
        var sourceFolderName = Path.GetFileName(Path.GetDirectoryName(sourceFilePath));
        var extractedFilePath = Path.Combine(
            extractPath,
            sourceFolderName!,
            Path.GetFileName(sourceFilePath)
        );

        File.Exists(extractedFilePath).ShouldBeTrue();
        File.ReadAllBytes(extractedFilePath).ShouldBe(File.ReadAllBytes(sourceFilePath));
    }

    public static void ExtractedFolderShouldMatchSourceFolder(
        string sourceFolderPath,
        string extractPath,
        string fileName
    )
    {
        var sourceFolderName = Path.GetFileName(
            Path.TrimEndingDirectorySeparator(sourceFolderPath)
        );
        var extractedFolderPath = Path.Combine(extractPath, sourceFolderName);
        var extractedFilePath = Path.Combine(extractedFolderPath, fileName);

        Directory.Exists(extractedFolderPath).ShouldBeTrue();
        File.Exists(extractedFilePath).ShouldBeTrue();
        File.ReadAllText(extractedFilePath)
            .ShouldBe(File.ReadAllText(Path.Combine(sourceFolderPath, fileName)));
    }

    public static async Task CreateNestedFoldersWithHiddenFilesAndEmptyFoldersAsync(
        string sourceFolderPath
    )
    {
        Directory.CreateDirectory(Path.Combine(sourceFolderPath, "Subs", "English"));
        Directory.CreateDirectory(Path.Combine(sourceFolderPath, "Subs", "Empty"));
        Directory.CreateDirectory(Path.Combine(sourceFolderPath, "Empty"));
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, "release.nfo"), "release");
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, ".hidden"), "hidden");
        await File.WriteAllTextAsync(
            Path.Combine(sourceFolderPath, "Subs", ".hidden-in-subfolder"),
            "hidden in subfolder"
        );
        await File.WriteAllTextAsync(
            Path.Combine(sourceFolderPath, "Subs", "English", "release.srt"),
            "subtitle"
        );
    }

    public static void ExtractedFolderShouldMatchSourceFolderStructure(
        string sourceFolderPath,
        string extractedFolderPath
    )
    {
        GetRelativeFolderPaths(extractedFolderPath)
            .ShouldBe(GetRelativeFolderPaths(sourceFolderPath));

        var sourceFilePaths = GetRelativeFilePaths(sourceFolderPath);
        GetRelativeFilePaths(extractedFolderPath).ShouldBe(sourceFilePaths);

        foreach (var relativeFilePath in sourceFilePaths)
        {
            File.ReadAllText(Path.Combine(extractedFolderPath, relativeFilePath))
                .ShouldBe(File.ReadAllText(Path.Combine(sourceFolderPath, relativeFilePath)));
        }
    }

    private static List<string> GetRelativeFolderPaths(string folderPath)
    {
        return Directory
            .GetDirectories(folderPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folderPath, path))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> GetRelativeFilePaths(string folderPath)
    {
        return Directory
            .GetFiles(folderPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(folderPath, path))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
