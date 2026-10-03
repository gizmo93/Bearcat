using System.Text.RegularExpressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Archivers.Shared;
using Microsoft.Extensions.Configuration;

namespace Bearcat.Archivers.Rar;

public partial class RarArchiver(
    IConfiguration configuration,
    ArchiverProcessRunner archiverProcessRunner
) : IArchiver, IArchiveExtractor
{
    public string Name => "RAR";

    public string FileExtension => ".rar";

    public bool CanChangeHashInPlace => true;

    public async Task<ArchiveResult> ArchiveAsync(
        string sourceFolderPath,
        string destinationPath,
        string archiveNamePrefix,
        int targetFileSizeMb,
        string? password,
        ArchiveOptions options,
        CancellationToken cancellationToken
    )
    {
        var arguments = CreateArchiveArguments(
            sourceFolderPath: sourceFolderPath,
            destinationPath: destinationPath,
            archiveNamePrefix: archiveNamePrefix,
            targetFileSizeMb: targetFileSizeMb,
            password: password,
            options: options
        );

        var processResult = await archiverProcessRunner.RunAsync(
            executablePath: GetExecutablePath(),
            arguments: arguments,
            archiverName: Name,
            cancellationToken: cancellationToken
        );

        if (processResult.ExitCode != 0)
        {
            return new ArchiveResult(
                IsSuccess: false,
                CreatedFileNames: [],
                ErrorMessages: processResult.ErrorLinesOrOutputLinesWhenNoErrorLines
            );
        }

        var createdFiles = CollectCreatedFiles(
            destinationPath: destinationPath,
            archiveNamePrefix: archiveNamePrefix
        );

        return new ArchiveResult(
            IsSuccess: true,
            CreatedFileNames: createdFiles,
            ErrorMessages: null
        );
    }

    public IReadOnlyList<ArchiveToExtract> FindArchivesToExtract(string folderPath)
    {
        var filePaths = Directory.GetFiles(folderPath).ToList();

        var newNamingArchives = FindNewNamingArchives(folderPath: folderPath, filePaths: filePaths);
        var oldNamingArchives = FindOldNamingArchives(folderPath: folderPath, filePaths: filePaths);

        return newNamingArchives
            .Concat(oldNamingArchives)
            .OrderBy(archive => archive.FirstVolumeFilePath, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ArchiveExtractionResult> ExtractAsync(
        ArchiveToExtract archiveToExtract,
        string destinationFolderPath,
        CancellationToken cancellationToken
    )
    {
        var destinationFolderPathWithTrailingSeparator =
            Path.TrimEndingDirectorySeparator(destinationFolderPath) + Path.DirectorySeparatorChar;

        List<string> arguments =
        [
            "x",
            "-o-",
            "-y",
            "-p-",
            archiveToExtract.FirstVolumeFilePath,
            destinationFolderPathWithTrailingSeparator,
        ];

        var processResult = await archiverProcessRunner.RunAsync(
            executablePath: GetExecutablePath(),
            arguments: arguments,
            archiverName: Name,
            cancellationToken: cancellationToken
        );

        return processResult.ExitCode == 0
            ? new ArchiveExtractionResult(IsSuccess: true, ErrorMessages: [])
            : new ArchiveExtractionResult(
                IsSuccess: false,
                ErrorMessages: processResult.ErrorLinesOrOutputLinesWhenNoErrorLines
            );
    }

    private static List<string> CollectCreatedFiles(
        string destinationPath,
        string archiveNamePrefix
    )
    {
        var directoryInfo = new DirectoryInfo(destinationPath);
        var files = directoryInfo.GetFiles($"{archiveNamePrefix}*.rar");

        return files.Select(f => f.FullName).ToList();
    }

    private static List<string> CreateArchiveArguments(
        string sourceFolderPath,
        string destinationPath,
        string archiveNamePrefix,
        int targetFileSizeMb,
        string? password,
        ArchiveOptions options
    )
    {
        var archiveFullPath = Path.Combine(destinationPath, archiveNamePrefix + ".rar");
        var compressionArgument = options.UseCompression ? "-m1" : "-m0";
        var solidArgument = options.UseSolidArchive ? "-s" : "-s-";
        var sourceArchivePath = Path.TrimEndingDirectorySeparator(sourceFolderPath);

        List<string> arguments =
        [
            "a",
            "-ep1",
            compressionArgument,
            solidArgument,
            $"-v{targetFileSizeMb}M",
        ];

        if (!options.PackSourceFolderAsRootFolder)
        {
            arguments.Add("-r");
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            arguments.Add($"-p{password}");
        }

        arguments.Add(archiveFullPath);
        arguments.Add(
            options.PackSourceFolderAsRootFolder
                ? sourceArchivePath
                : Path.Join(sourceArchivePath, "*")
        );

        return arguments;
    }

    private static List<ArchiveToExtract> FindNewNamingArchives(
        string folderPath,
        List<string> filePaths
    )
    {
        var volumeFiles = new List<NewNamingVolumeFile>();

        foreach (var filePath in filePaths)
        {
            var match = NewNamingVolumeRegex().Match(Path.GetFileName(filePath));
            if (match.Success)
            {
                volumeFiles.Add(
                    new NewNamingVolumeFile(
                        FilePath: filePath,
                        BaseName: match.Groups["baseName"].Value,
                        PartNumber: int.Parse(match.Groups["partNumber"].Value)
                    )
                );
            }
        }

        return volumeFiles
            .GroupBy(volumeFile => volumeFile.BaseName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                CreateNewNamingArchive(
                    folderPath: folderPath,
                    baseName: group.Key,
                    volumeFilesOfArchive: group.ToList()
                )
            )
            .ToList();
    }

    private static ArchiveToExtract CreateNewNamingArchive(
        string folderPath,
        string baseName,
        List<NewNamingVolumeFile> volumeFilesOfArchive
    )
    {
        var firstVolumeFile = volumeFilesOfArchive.SingleOrDefault(volumeFile =>
            volumeFile.PartNumber == 1
        );

        if (firstVolumeFile is null)
        {
            throw CreateFirstVolumeMissingException(folderPath: folderPath, baseName: baseName);
        }

        var volumeFilePaths = volumeFilesOfArchive
            .OrderBy(volumeFile => volumeFile.PartNumber)
            .Select(volumeFile => volumeFile.FilePath)
            .ToList();

        return new ArchiveToExtract(
            FirstVolumeFilePath: firstVolumeFile.FilePath,
            VolumeFilePaths: volumeFilePaths
        );
    }

    private static List<ArchiveToExtract> FindOldNamingArchives(
        string folderPath,
        List<string> filePaths
    )
    {
        var oldNamingFilePaths = filePaths
            .Where(filePath => !NewNamingVolumeRegex().IsMatch(Path.GetFileName(filePath)))
            .ToList();

        var firstVolumeFilePathsByBaseName = oldNamingFilePaths
            .Select(filePath =>
                (
                    FilePath: filePath,
                    Match: OldNamingFirstVolumeRegex().Match(Path.GetFileName(filePath))
                )
            )
            .Where(fileMatch => fileMatch.Match.Success)
            .ToDictionary(
                fileMatch => fileMatch.Match.Groups["baseName"].Value,
                fileMatch => fileMatch.FilePath,
                StringComparer.OrdinalIgnoreCase
            );

        var continuationVolumeFilePathsByBaseName = oldNamingFilePaths
            .Select(filePath =>
                (
                    FilePath: filePath,
                    Match: OldNamingContinuationVolumeRegex().Match(Path.GetFileName(filePath))
                )
            )
            .Where(fileMatch => fileMatch.Match.Success)
            .GroupBy(
                fileMatch => fileMatch.Match.Groups["baseName"].Value,
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                group => group.Key,
                group => group.Select(fileMatch => fileMatch.FilePath).ToList(),
                StringComparer.OrdinalIgnoreCase
            );

        var baseNameWithoutFirstVolume = continuationVolumeFilePathsByBaseName.Keys.FirstOrDefault(
            baseName => !firstVolumeFilePathsByBaseName.ContainsKey(baseName)
        );

        if (baseNameWithoutFirstVolume is not null)
        {
            throw CreateFirstVolumeMissingException(
                folderPath: folderPath,
                baseName: baseNameWithoutFirstVolume
            );
        }

        return firstVolumeFilePathsByBaseName
            .Select(firstVolume =>
                CreateOldNamingArchive(
                    firstVolumeFilePath: firstVolume.Value,
                    continuationVolumeFilePaths: continuationVolumeFilePathsByBaseName.GetValueOrDefault(
                        firstVolume.Key,
                        []
                    )
                )
            )
            .ToList();
    }

    private static ArchiveToExtract CreateOldNamingArchive(
        string firstVolumeFilePath,
        List<string> continuationVolumeFilePaths
    )
    {
        var orderedContinuationVolumeFilePaths = continuationVolumeFilePaths.OrderBy(
            filePath => Path.GetExtension(filePath),
            StringComparer.OrdinalIgnoreCase
        );

        return new ArchiveToExtract(
            FirstVolumeFilePath: firstVolumeFilePath,
            VolumeFilePaths: [firstVolumeFilePath, .. orderedContinuationVolumeFilePaths]
        );
    }

    private static InvalidOperationException CreateFirstVolumeMissingException(
        string folderPath,
        string baseName
    )
    {
        return new InvalidOperationException(
            $"The folder '{folderPath}' contains volumes of the RAR archive '{baseName}', but its first volume is missing."
        );
    }

    private string GetExecutablePath()
    {
        var configuredPath = configuration["Archivers:RarPath"];
        return string.IsNullOrWhiteSpace(configuredPath) ? "rar" : configuredPath;
    }

    [GeneratedRegex(@"^(?<baseName>.+)\.part(?<partNumber>[0-9]+)\.rar$", RegexOptions.IgnoreCase)]
    private static partial Regex NewNamingVolumeRegex();

    [GeneratedRegex(@"^(?<baseName>.+)\.rar$", RegexOptions.IgnoreCase)]
    private static partial Regex OldNamingFirstVolumeRegex();

    [GeneratedRegex(@"^(?<baseName>.+)\.[r-y][0-9]{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex OldNamingContinuationVolumeRegex();

    private sealed record NewNamingVolumeFile(string FilePath, string BaseName, int PartNumber);
}
