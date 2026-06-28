using Bearcat.Abstractions;
using Bearcat.Abstractions.Media;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class MediaMetadataService(
    IMediaMetadataRepository repository,
    IMediaMetadataExtractor extractor,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider,
    ILogger<MediaMetadataService> logger
)
{
    public async Task TryExtractAsync(
        Release release,
        CancellationToken cancellationToken = default
    )
    {
        if (release.ReleaseType != ReleaseType.Managed)
        {
            return;
        }

        release.MediaFiles = await BuildMediaFilesAsync(
            release.ReleaseFolderPath,
            cancellationToken
        );
        release.MediaMetadataExtractedAt = timeProvider.GetLocalNow();
    }

    public async Task ExtractForReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseWithMediaFilesAsync(releaseId, cancellationToken);

        if (release?.ReleaseType is not ReleaseType.Managed)
        {
            return;
        }

        foreach (var existingMediaFile in release.MediaFiles.ToList())
        {
            repository.RemoveMediaFile(existingMediaFile);
        }

        release.MediaFiles = await BuildMediaFilesAsync(
            release.ReleaseFolderPath,
            cancellationToken
        );
        release.MediaMetadataExtractedAt = timeProvider.GetLocalNow();

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<ReleaseMediaFile>> BuildMediaFilesAsync(
        string? releaseFolderPath,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(releaseFolderPath) || !Directory.Exists(releaseFolderPath))
        {
            return [];
        }

        List<string> filePaths;

        try
        {
            filePaths = fileSystemService
                .GetFilesInPath(releaseFolderPath, recursive: true)
                .Where(MediaFileTypes.IsVideoFile)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Failed to enumerate media files in {ReleaseFolderPath}",
                releaseFolderPath
            );
            return [];
        }

        var mediaFiles = new List<ReleaseMediaFile>();

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var probe = await extractor.ExtractAsync(filePath, cancellationToken);

            if (probe is null)
            {
                continue;
            }

            mediaFiles.Add(
                new ReleaseMediaFile
                {
                    RelativePath = Path.GetRelativePath(releaseFolderPath, filePath),
                    SizeBytes = MediaInfoOutputParser.Parse(probe.Json)?.SizeBytes ?? 0,
                    MediaInfoJson = probe.Json,
                    MediaInfoText = probe.Text,
                }
            );
        }

        return mediaFiles;
    }
}
