using System.Diagnostics;
using System.Text;
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

    public static async Task ExtractWithSevenZipAsync(
        string archiveFileName,
        string extractPath,
        CancellationToken cancellationToken = default
    )
    {
        await RunProcessAsync(
            fileName: "7z",
            arguments: ["x", "-y", $"-o{extractPath}", archiveFileName],
            cancellationToken: cancellationToken,
            successfulExitCodes: [0, 1]
        );
    }

    public static async Task ExtractWithRarAsync(
        string archiveFileName,
        string extractPath,
        CancellationToken cancellationToken = default
    )
    {
        await RunProcessAsync(
            fileName: "rar",
            arguments: ["x", "-y", archiveFileName, extractPath + Path.DirectorySeparatorChar],
            cancellationToken: cancellationToken,
            successfulExitCodes: [0]
        );
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

    private static async Task RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyList<int> successfulExitCodes,
        CancellationToken cancellationToken
    )
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = new Process();
        process.StartInfo = processStartInfo;

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                error.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        successfulExitCodes.ShouldContain(
            process.ExitCode,
            $"Process {fileName} {string.Join(" ", arguments)} failed."
                + $"{Environment.NewLine}Output: {output}"
                + $"{Environment.NewLine}Error: {error}"
        );
    }
}
