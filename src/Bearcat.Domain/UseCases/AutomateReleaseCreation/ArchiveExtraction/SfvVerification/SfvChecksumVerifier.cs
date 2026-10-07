using System.Globalization;
using System.IO.Hashing;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.SfvVerification;

public class SfvChecksumVerifier(
    ITransferProgressTracker progressTracker,
    ILogger<SfvChecksumVerifier> logger
)
{
    private const int Crc32HexLength = 8;
    private const int MaxListedFilePaths = 10;
    private const int ReadBufferSizeBytes = 1024 * 1024;

    public static IReadOnlyList<string> FindSfvFiles(string folderPath)
    {
        return Directory
            .GetFiles(
                path: folderPath,
                searchPattern: "*.sfv",
                enumerationOptions: new EnumerationOptions
                {
                    AttributesToSkip = FileAttributes.None,
                    RecurseSubdirectories = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                }
            )
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<SfvFile>> VerifyAsync(
        string folderPath,
        IReadOnlyList<string> sfvFilePaths,
        TransferIdentifier transferIdentifier,
        string sourceName,
        CancellationToken cancellationToken
    )
    {
        var sfvFiles = new List<SfvFile>();

        foreach (var sfvFilePath in sfvFilePaths)
        {
            sfvFiles.Add(await ParseSfvFileAsync(sfvFilePath, folderPath, cancellationToken));
        }

        var entriesToVerify = sfvFiles
            .SelectMany(sfvFile => sfvFile.Entries)
            .Select((entry, index) => new EntryToVerify(index + 1, entry))
            .ToList();

        progressTracker.StartTracking(
            transferIdentifier,
            entriesToVerify
                .Select(entryToVerify => new TransferFile(
                    FileId: entryToVerify.FileId,
                    FileName: entryToVerify.Entry.RelativeFilePath,
                    SourceName: sourceName,
                    SizeBytes: GetFileSizeBytesOrNullWhenMissing(entryToVerify.Entry.FilePath),
                    IsAlreadyTransferred: false
                ))
                .ToList()
        );

        try
        {
            await VerifyEntriesAsync(
                entriesToVerify: entriesToVerify,
                transferIdentifier: transferIdentifier,
                sourceName: sourceName,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            progressTracker.StopTracking(transferIdentifier);
        }

        logger.LogInformation(
            "Verified the CRC32 checksums of {FileCount} files listed in {SfvFileCount} SFV files in {FolderPath}",
            entriesToVerify.Count,
            sfvFiles.Count,
            folderPath
        );

        return sfvFiles;
    }

    private async Task VerifyEntriesAsync(
        List<EntryToVerify> entriesToVerify,
        TransferIdentifier transferIdentifier,
        string sourceName,
        CancellationToken cancellationToken
    )
    {
        var missingRelativeFilePaths = new List<string>();
        var mismatchingRelativeFilePaths = new List<string>();

        foreach (var (fileId, entry) in entriesToVerify)
        {
            if (!File.Exists(entry.FilePath))
            {
                missingRelativeFilePaths.Add(entry.RelativeFilePath);
                continue;
            }

            var progress = new TransferProgressReporter(
                tracker: progressTracker,
                identifier: transferIdentifier,
                fileId: fileId,
                fileName: entry.RelativeFilePath,
                sourceName: sourceName
            );

            if (
                await ComputeCrc32Async(entry.FilePath, progress, cancellationToken)
                != entry.ExpectedCrc32
            )
            {
                mismatchingRelativeFilePaths.Add(entry.RelativeFilePath);
            }
        }

        if (missingRelativeFilePaths.Count > 0 || mismatchingRelativeFilePaths.Count > 0)
        {
            throw new InvalidDataException(
                CreateVerificationFailedMessage(
                    missingRelativeFilePaths,
                    mismatchingRelativeFilePaths
                )
            );
        }
    }

    private static long? GetFileSizeBytesOrNullWhenMissing(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        return fileInfo.Exists ? fileInfo.Length : null;
    }

    private static async Task<SfvFile> ParseSfvFileAsync(
        string sfvFilePath,
        string folderPath,
        CancellationToken cancellationToken
    )
    {
        var lines = await File.ReadAllLinesAsync(sfvFilePath, cancellationToken);
        var sfvRelativeFilePath = GetRelativePath(folderPath, sfvFilePath);

        var sfvRelativeFolderPath = GetRelativePath(
            folderPath,
            Path.GetDirectoryName(sfvFilePath)!
        );

        var entries = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith(';'))
            .Select(line =>
                ParseSfvLine(
                    line: line,
                    folderPath: folderPath,
                    sfvRelativeFilePath: sfvRelativeFilePath,
                    sfvRelativeFolderPath: sfvRelativeFolderPath
                )
            )
            .ToList();

        return new SfvFile(sfvFilePath, entries);
    }

    private static SfvEntry ParseSfvLine(
        string line,
        string folderPath,
        string sfvRelativeFilePath,
        string sfvRelativeFolderPath
    )
    {
        var separatorIndex = line.LastIndexOfAny([' ', '\t']);

        if (
            separatorIndex <= 0
            || !TryParseCrc32(line[(separatorIndex + 1)..], out var expectedCrc32)
        )
        {
            throw new InvalidDataException(
                $"The line '{line}' in the SFV file '{sfvRelativeFilePath}' is not a file name followed by a CRC32 checksum"
            );
        }

        var fileName = line[..separatorIndex].TrimEnd().Replace('\\', '/');
        var relativeFilePath =
            sfvRelativeFolderPath == "." ? fileName : $"{sfvRelativeFolderPath}/{fileName}";

        var filePath =
            RemoteDownloadPaths.GetSafeLocalFilePath(folderPath, relativeFilePath)
            ?? throw new InvalidDataException(
                $"The SFV file '{sfvRelativeFilePath}' references the unsafe path '{fileName}' that is not inside the folder"
            );

        return new SfvEntry(filePath, relativeFilePath, expectedCrc32);
    }

    private static bool TryParseCrc32(string value, out uint crc32)
    {
        crc32 = 0;

        return value.Length == Crc32HexLength
            && uint.TryParse(
                value,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out crc32
            );
    }

    private static string GetRelativePath(string folderPath, string path)
    {
        return Path.GetRelativePath(folderPath, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static async Task<uint> ComputeCrc32Async(
        string filePath,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var crc32 = new Crc32();
        var buffer = new byte[ReadBufferSizeBytes];

        await using var stream = new FileStream(
            path: filePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: 0,
            useAsync: true
        );

        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            crc32.Append(buffer.AsSpan(0, bytesRead));
            progress.ReportBytesTransferred(bytesRead);
        }

        return crc32.GetCurrentHashAsUInt32();
    }

    private static string CreateVerificationFailedMessage(
        List<string> missingRelativeFilePaths,
        List<string> mismatchingRelativeFilePaths
    )
    {
        var problems = new List<string>();

        if (missingRelativeFilePaths.Count > 0)
        {
            problems.Add($"missing files: {FormatFilePaths(missingRelativeFilePaths)}");
        }

        if (mismatchingRelativeFilePaths.Count > 0)
        {
            problems.Add(
                $"files with a wrong CRC32 checksum: {FormatFilePaths(mismatchingRelativeFilePaths)}"
            );
        }

        return $"The SFV verification failed, {string.Join("; ", problems)}";
    }

    private static string FormatFilePaths(List<string> relativeFilePaths)
    {
        var listedFilePaths = string.Join(", ", relativeFilePaths.Take(MaxListedFilePaths));

        return relativeFilePaths.Count > MaxListedFilePaths
            ? $"{listedFilePaths} and {relativeFilePaths.Count - MaxListedFilePaths} more"
            : listedFilePaths;
    }

    private sealed record EntryToVerify(int FileId, SfvEntry Entry);
}
