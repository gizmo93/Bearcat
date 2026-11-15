using System.Globalization;
using Bearcat.Abstractions.Media;
using Bearcat.Media.Ffmpeg;

namespace Bearcat.Media.Screenshots;

public sealed class FfmpegVideoScreenshotCreator(IFfmpegInstaller ffmpegInstaller)
    : IVideoScreenshotCreator
{
    private const string TileFileNamePattern = "tile-%03d.jpg";

    public async Task<IReadOnlyList<CreatedScreenshotFile>> CreateScreenshotsAsync(
        ScreenshotCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        var executablePath = await ffmpegInstaller.EnsureInstalledAsync(cancellationToken);
        var timestamps = ScreenshotTimestampCalculator.Calculate(request.Duration, request.Count);
        var createdFiles = new List<CreatedScreenshotFile>();

        Directory.CreateDirectory(request.OutputDirectory);

        for (var index = 0; index < timestamps.Count; index++)
        {
            var outputFilePath = Path.Combine(
                request.OutputDirectory,
                $"screenshot-{index + 1:00}.jpg"
            );

            var arguments = FfmpegArgumentBuilder.BuildScreenshotArguments(
                videoFilePath: request.VideoFilePath,
                timestamp: timestamps[index],
                transferCharacteristic: request.TransferCharacteristic,
                outputFilePath: outputFilePath
            );

            await FfmpegProcessRunner.RunAsync(
                executablePath: executablePath,
                arguments: arguments,
                workingDirectory: null,
                cancellationToken: cancellationToken
            );

            EnsureFrameWasWritten(outputFilePath, timestamps[index]);
            var (width, height) = JpegDimensionReader.Read(outputFilePath);

            createdFiles.Add(
                new CreatedScreenshotFile(
                    FilePath: outputFilePath,
                    Timestamp: timestamps[index],
                    Width: width,
                    Height: height,
                    SizeBytes: new FileInfo(outputFilePath).Length
                )
            );
        }

        return createdFiles;
    }

    public async Task<CreatedScreenshotGridFile> CreateScreenshotGridAsync(
        ScreenshotGridCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        var executablePath = await ffmpegInstaller.EnsureInstalledAsync(cancellationToken);

        var layout = ScreenshotGridLayout.Calculate(
            columns: request.Columns,
            rows: request.Rows,
            sheetWidthPixels: request.SheetWidthPixels,
            headerLineCount: request.HeaderLines.Count
        );

        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"bearcat-screenshot-grid-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(workingDirectory);

        try
        {
            await ScreenshotGridFont.WriteToDirectoryAsync(workingDirectory, cancellationToken);

            await CreateScreenshotGridTilesAsync(
                executablePath: executablePath,
                request: request,
                layout: layout,
                workingDirectory: workingDirectory,
                cancellationToken: cancellationToken
            );

            var headerTextFileNames = await WriteHeaderTextFilesAsync(
                headerLines: request.HeaderLines,
                workingDirectory: workingDirectory,
                cancellationToken: cancellationToken
            );

            var outputFilePath = Path.GetFullPath(request.OutputFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

            var arguments = FfmpegArgumentBuilder.BuildScreenshotGridAssemblyArguments(
                layout: layout,
                tileFileNamePattern: TileFileNamePattern,
                headerTextFileNames: headerTextFileNames,
                outputFilePath: outputFilePath
            );

            await FfmpegProcessRunner.RunAsync(
                executablePath: executablePath,
                arguments: arguments,
                workingDirectory: workingDirectory,
                cancellationToken: cancellationToken
            );

            var (width, height) = JpegDimensionReader.Read(outputFilePath);

            return new CreatedScreenshotGridFile(
                FilePath: outputFilePath,
                Width: width,
                Height: height,
                SizeBytes: new FileInfo(outputFilePath).Length
            );
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static async Task CreateScreenshotGridTilesAsync(
        string executablePath,
        ScreenshotGridCreationRequest request,
        ScreenshotGridLayout layout,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        var timestamps = ScreenshotTimestampCalculator.Calculate(
            request.Duration,
            request.Columns * request.Rows
        );

        for (var index = 0; index < timestamps.Count; index++)
        {
            var timestampTextFileName = $"timestamp-{index + 1:000}.txt";
            var tileFileName = $"tile-{index + 1:000}.jpg";

            await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, timestampTextFileName),
                timestamps[index].ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
                cancellationToken
            );

            var arguments = FfmpegArgumentBuilder.BuildScreenshotGridTileArguments(
                videoFilePath: request.VideoFilePath,
                timestamp: timestamps[index],
                transferCharacteristic: request.TransferCharacteristic,
                layout: layout,
                timestampTextFileName: timestampTextFileName,
                outputFileName: tileFileName
            );

            await FfmpegProcessRunner.RunAsync(
                executablePath: executablePath,
                arguments: arguments,
                workingDirectory: workingDirectory,
                cancellationToken: cancellationToken
            );

            EnsureFrameWasWritten(Path.Combine(workingDirectory, tileFileName), timestamps[index]);
        }
    }

    private static async Task<List<string>> WriteHeaderTextFilesAsync(
        IReadOnlyList<string> headerLines,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        var headerTextFileNames = new List<string>();

        for (var index = 0; index < headerLines.Count; index++)
        {
            var headerTextFileName = $"header-{index + 1:00}.txt";

            await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, headerTextFileName),
                headerLines[index],
                cancellationToken
            );

            headerTextFileNames.Add(headerTextFileName);
        }

        return headerTextFileNames;
    }

    private static void EnsureFrameWasWritten(string outputFilePath, TimeSpan timestamp)
    {
        if (!File.Exists(outputFilePath))
        {
            throw new InvalidOperationException(
                $"ffmpeg did not write a frame at {timestamp} to {outputFilePath}."
            );
        }
    }
}
