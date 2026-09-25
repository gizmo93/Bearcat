using System.Text;
using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;

public class ReleaseFolderEntriesForPackingService(IFileSystemService fileSystemService)
{
    public const string NonceFileName = "__nonce.txt";

    private const string NonceFileDescription = "the nonce file";

    private static readonly UTF8Encoding Utf8EncodingWithByteOrderMark = new(
        encoderShouldEmitUTF8Identifier: true
    );

    public ReleaseFolderEntriesForPackingResult GetReleaseFolderEntriesForPacking(
        string releaseFolderPath,
        IReadOnlyList<AdditionalArchiveContent> additionalArchiveContents
    )
    {
        var entries = new List<ReleaseFolderEntryForPacking> { CreateNonceEntry() };
        var errorMessages = new List<string>();

        foreach (var content in additionalArchiveContents)
        {
            var sourcePathErrorMessage = ValidateSourcePath(releaseFolderPath, content);

            if (sourcePathErrorMessage is not null)
            {
                errorMessages.Add(sourcePathErrorMessage);
                continue;
            }

            entries.Add(CreateEntry(content));
        }

        errorMessages.AddRange(ValidateDuplicatedEntryNames(entries));
        errorMessages.AddRange(ValidateAlreadyExistingEntries(releaseFolderPath, entries));

        return errorMessages.Count == 0
            ? new ReleaseFolderEntriesForPackingResult(entries, [])
            : new ReleaseFolderEntriesForPackingResult([], errorMessages);
    }

    public async Task CopyIntoReleaseFolderAsync(
        string releaseFolderPath,
        IReadOnlyList<ReleaseFolderEntryForPacking> entries,
        CancellationToken cancellationToken
    )
    {
        foreach (var entry in entries)
        {
            var targetPath = Path.Join(releaseFolderPath, entry.Name);

            switch (entry.Type)
            {
                case ReleaseFolderEntryForPackingType.FileCopy:
                    fileSystemService.CopyFile(entry.SourcePath!, targetPath);
                    break;
                case ReleaseFolderEntryForPackingType.DirectoryCopy:
                    fileSystemService.CopyDirectoryRecursively(entry.SourcePath!, targetPath);
                    break;
                case ReleaseFolderEntryForPackingType.TextFile:
                    await File.WriteAllTextAsync(
                        path: targetPath,
                        contents: entry.TextContent,
                        encoding: Utf8EncodingWithByteOrderMark,
                        cancellationToken: cancellationToken
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(entries),
                        $"Unknown release folder entry type, {entry.Type}"
                    );
            }
        }
    }

    public void DeleteFromReleaseFolder(string releaseFolderPath, IReadOnlyList<string> entryNames)
    {
        foreach (var entryName in entryNames)
        {
            var entryPath = Path.Join(releaseFolderPath, entryName);

            fileSystemService.DeleteFileIfExists(entryPath);
            fileSystemService.DeleteDirectoryIfExists(entryPath);
        }
    }

    private static ReleaseFolderEntryForPacking CreateNonceEntry()
    {
        return new ReleaseFolderEntryForPacking(
            Name: NonceFileName,
            SourceDescription: NonceFileDescription,
            Type: ReleaseFolderEntryForPackingType.TextFile,
            SourcePath: null,
            TextContent: Guid.NewGuid().ToString()
        );
    }

    private string? ValidateSourcePath(string releaseFolderPath, AdditionalArchiveContent content)
    {
        if (content.Type is AdditionalArchiveContentType.TextFile)
        {
            return null;
        }

        var sourcePath = Path.GetFullPath(content.SourcePath!);

        if (fileSystemService.FileExists(sourcePath))
        {
            return null;
        }

        if (!fileSystemService.DirectoryExists(sourcePath))
        {
            return $"Cannot place {DescribeContent(content)} into the release folder because its source path \"{content.SourcePath}\" does not exist.";
        }

        if (FolderPathHelper.IsSameOrSubPath(childPath: releaseFolderPath, parentPath: sourcePath))
        {
            return $"Cannot place {DescribeContent(content)} into the release folder because its source path \"{content.SourcePath}\" contains the release folder \"{releaseFolderPath}\".";
        }

        return null;
    }

    private ReleaseFolderEntryForPacking CreateEntry(AdditionalArchiveContent content)
    {
        return content.Type switch
        {
            AdditionalArchiveContentType.Path => CreatePathEntry(content),
            AdditionalArchiveContentType.TextFile => new ReleaseFolderEntryForPacking(
                Name: content.FileName!,
                SourceDescription: DescribeContent(content),
                Type: ReleaseFolderEntryForPackingType.TextFile,
                SourcePath: null,
                TextContent: content.TextContent!.ReplaceLineEndings("\r\n")
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(content),
                $"Unknown additional archive content type, {content.Type}"
            ),
        };
    }

    private ReleaseFolderEntryForPacking CreatePathEntry(AdditionalArchiveContent content)
    {
        var sourcePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(content.SourcePath!));

        return new ReleaseFolderEntryForPacking(
            Name: Path.GetFileName(sourcePath),
            SourceDescription: DescribeContent(content),
            Type: fileSystemService.FileExists(sourcePath)
                ? ReleaseFolderEntryForPackingType.FileCopy
                : ReleaseFolderEntryForPackingType.DirectoryCopy,
            SourcePath: sourcePath,
            TextContent: null
        );
    }

    private static List<string> ValidateDuplicatedEntryNames(
        List<ReleaseFolderEntryForPacking> entries
    )
    {
        return entries
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"Cannot place {string.Join(" and ", group.Select(entry => entry.SourceDescription))} into the release folder because they would all be named \"{group.Key}\"."
            )
            .ToList();
    }

    private List<string> ValidateAlreadyExistingEntries(
        string releaseFolderPath,
        List<ReleaseFolderEntryForPacking> entries
    )
    {
        var existingEntryNames = fileSystemService
            .GetFilesInPath(releaseFolderPath, recursive: false)
            .Concat(fileSystemService.GetFoldersInPath(releaseFolderPath))
            .Select(path => Path.GetFileName(path))
            .Where(name => !string.Equals(name, NonceFileName, StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return entries
            .Where(entry => existingEntryNames.Contains(entry.Name))
            .Select(entry =>
                $"Cannot place {entry.SourceDescription} into the release folder because \"{entry.Name}\" already exists in \"{releaseFolderPath}\"."
            )
            .ToList();
    }

    private static string DescribeContent(AdditionalArchiveContent content)
    {
        return $"additional archive content \"{content.Name}\"";
    }
}
