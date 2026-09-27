using System.Globalization;
using System.IO.Hashing;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.SfvVerification;

public class SfvChecksumVerifier(ILogger<SfvChecksumVerifier> logger)
{
    private const int Crc32HexLength = 8;
    private const int MaxListedFilePaths = 10;
    private const int ReadBufferSizeBytes = 1024 * 1024;

    public IReadOnlyList<string> FindSfvFiles(string localFolderPath)
    {
        return Directory
            .GetFiles(
                path: localFolderPath,
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
        string localFolderPath,
        IReadOnlyList<string> sfvFilePaths,
        CancellationToken cancellationToken
    )
    {
        var sfvFiles = new List<SfvFile>();
        var missingRelativeFilePaths = new List<string>();
        var mismatchingRelativeFilePaths = new List<string>();

        foreach (var sfvFilePath in sfvFilePaths)
        {
            var sfvFile = await ParseSfvFileAsync(sfvFilePath, localFolderPath, cancellationToken);
            sfvFiles.Add(sfvFile);

            foreach (var entry in sfvFile.Entries)
            {
                if (!File.Exists(entry.FilePath))
                {
                    missingRelativeFilePaths.Add(entry.RelativeFilePath);
                }
                else if (
                    await ComputeCrc32Async(entry.FilePath, cancellationToken)
                    != entry.ExpectedCrc32
                )
                {
                    mismatchingRelativeFilePaths.Add(entry.RelativeFilePath);
                }
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

        logger.LogInformation(
            "Verified the CRC32 checksums of {FileCount} files listed in {SfvFileCount} SFV files in {LocalFolderPath}",
            sfvFiles.Sum(sfvFile => sfvFile.Entries.Count),
            sfvFiles.Count,
            localFolderPath
        );

        return sfvFiles;
    }

    private static async Task<SfvFile> ParseSfvFileAsync(
        string sfvFilePath,
        string localFolderPath,
        CancellationToken cancellationToken
    )
    {
        var lines = await File.ReadAllLinesAsync(sfvFilePath, cancellationToken);
        var sfvRelativeFilePath = GetRelativePath(localFolderPath, sfvFilePath);

        var sfvRelativeFolderPath = GetRelativePath(
            localFolderPath,
            Path.GetDirectoryName(sfvFilePath)!
        );

        var entries = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith(';'))
            .Select(line =>
                ParseSfvLine(
                    line: line,
                    localFolderPath: localFolderPath,
                    sfvRelativeFilePath: sfvRelativeFilePath,
                    sfvRelativeFolderPath: sfvRelativeFolderPath
                )
            )
            .ToList();

        return new SfvFile(sfvFilePath, entries);
    }

    private static SfvEntry ParseSfvLine(
        string line,
        string localFolderPath,
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
            RemoteDownloadPaths.GetSafeLocalFilePath(localFolderPath, relativeFilePath)
            ?? throw new InvalidDataException(
                $"The SFV file '{sfvRelativeFilePath}' references the unsafe path '{fileName}' that is not inside the download folder"
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

    private static string GetRelativePath(string localFolderPath, string path)
    {
        return Path.GetRelativePath(localFolderPath, path)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static async Task<uint> ComputeCrc32Async(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        var crc32 = new Crc32();

        await using var stream = new FileStream(
            path: filePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: ReadBufferSizeBytes,
            useAsync: true
        );
        await crc32.AppendAsync(stream, cancellationToken);

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
}
