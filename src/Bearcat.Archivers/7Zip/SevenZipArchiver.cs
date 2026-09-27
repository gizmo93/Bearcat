using System.Text.RegularExpressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Archivers.Shared;
using Microsoft.Extensions.Configuration;

namespace Bearcat.Archivers._7Zip;

public partial class SevenZipArchiver(
    IConfiguration configuration,
    ArchiverProcessRunner archiverProcessRunner
) : IArchiver, IArchiveExtractor
{
    public string Name => "7Zip";

    public string FileExtension => ".7z";

    public bool CanChangeHashInPlace => false;

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
        var singleVolumeArchives = new List<ArchiveToExtract>();
        var volumeFiles = new List<MultiVolumeFile>();

        foreach (var filePath in Directory.GetFiles(folderPath))
        {
            var fileName = Path.GetFileName(filePath);

            if (SingleVolumeRegex().IsMatch(fileName))
            {
                singleVolumeArchives.Add(
                    new ArchiveToExtract(FirstVolumeFilePath: filePath, VolumeFilePaths: [filePath])
                );
                continue;
            }

            var volumeMatch = MultiVolumeRegex().Match(fileName);
            if (volumeMatch.Success)
            {
                volumeFiles.Add(
                    new MultiVolumeFile(
                        FilePath: filePath,
                        BaseName: volumeMatch.Groups["baseName"].Value,
                        VolumeNumber: int.Parse(volumeMatch.Groups["volumeNumber"].Value)
                    )
                );
            }
        }

        var multiVolumeArchives = volumeFiles
            .GroupBy(volumeFile => volumeFile.BaseName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                CreateMultiVolumeArchive(
                    folderPath: folderPath,
                    baseName: group.Key,
                    volumeFilesOfArchive: group.ToList()
                )
            );

        return singleVolumeArchives
            .Concat(multiVolumeArchives)
            .OrderBy(archive => archive.FirstVolumeFilePath, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ArchiveExtractionResult> ExtractAsync(
        ArchiveToExtract archiveToExtract,
        string destinationFolderPath,
        CancellationToken cancellationToken
    )
    {
        List<string> arguments =
        [
            "x",
            "-y",
            "-aos",
            "-p",
            $"-o{destinationFolderPath}",
            archiveToExtract.FirstVolumeFilePath,
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
        var files = directoryInfo.GetFiles($"{archiveNamePrefix}.7z*");

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
        var archiveFullPath = Path.Combine(destinationPath, archiveNamePrefix + ".7z");
        var compressionArgument = options.UseCompression ? "-mx=1" : "-mx=0";
        var solidArgument = options.UseSolidArchive ? "-ms=on" : "-ms=off";
        var sourceArchivePath = Path.TrimEndingDirectorySeparator(sourceFolderPath);

        List<string> arguments =
        [
            "a",
            $"-v{targetFileSizeMb}m",
            compressionArgument,
            solidArgument,
        ];

        if (!string.IsNullOrWhiteSpace(password))
        {
            arguments.Add($"-p{password}");
        }

        arguments.Add(archiveFullPath);
        arguments.Add(sourceArchivePath);

        return arguments;
    }

    private static ArchiveToExtract CreateMultiVolumeArchive(
        string folderPath,
        string baseName,
        List<MultiVolumeFile> volumeFilesOfArchive
    )
    {
        var firstVolumeFile = volumeFilesOfArchive.SingleOrDefault(volumeFile =>
            volumeFile.VolumeNumber == 1
        );

        if (firstVolumeFile is null)
        {
            throw new InvalidOperationException(
                $"The folder '{folderPath}' contains volumes of the 7z archive '{baseName}', but its first volume is missing."
            );
        }

        var volumeFilePaths = volumeFilesOfArchive
            .OrderBy(volumeFile => volumeFile.VolumeNumber)
            .Select(volumeFile => volumeFile.FilePath)
            .ToList();

        return new ArchiveToExtract(
            FirstVolumeFilePath: firstVolumeFile.FilePath,
            VolumeFilePaths: volumeFilePaths
        );
    }

    private string GetExecutablePath()
    {
        var configuredPath = configuration["Archivers:SevenZipPath"];
        return string.IsNullOrWhiteSpace(configuredPath) ? "7z" : configuredPath;
    }

    [GeneratedRegex(@"^.+\.7z$", RegexOptions.IgnoreCase)]
    private static partial Regex SingleVolumeRegex();

    [GeneratedRegex(@"^(?<baseName>.+)\.7z\.(?<volumeNumber>[0-9]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MultiVolumeRegex();

    private sealed record MultiVolumeFile(string FilePath, string BaseName, int VolumeNumber);
}
