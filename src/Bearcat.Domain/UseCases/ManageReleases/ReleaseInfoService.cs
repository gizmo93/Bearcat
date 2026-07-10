using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseInfoService(
    IReleaseInfoRepository repository,
    ILogger<ReleaseInfoService> logger
)
{
    public async Task DeleteAsync(int releaseInfoId, CancellationToken cancellationToken = default)
    {
        var releaseInfo = await repository.GetReleaseInfoByIdAsync(
            releaseInfoId: releaseInfoId,
            cancellationToken: cancellationToken
        );
        repository.Remove(releaseInfo);

        if (releaseInfo.Release.Metadata is not null)
        {
            repository.Remove(releaseInfo.Release.Metadata);
        }

        releaseInfo.Release.ReleaseInfoCheckedAt = null;
        releaseInfo.Release.MetadataCheckedAt = null;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateReleaseInfoAsync(
        int releaseId,
        EditReleaseInfoData data,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseForCoverUpdateAsync(releaseId, cancellationToken);
        var newCoverUrl = CleanOptional(data.CoverUrl);
        var previousCoverUrl = release.Metadata?.CoverUrl;

        var releaseInfo = release.ReleaseInfo;
        if (releaseInfo is null)
        {
            releaseInfo = ReleaseInfo.CreatePlaceholder(ReleaseInfo.ManualSource, release.Name);
            release.ReleaseInfo = releaseInfo;
        }

        releaseInfo.ReleaseName = CleanOptional(data.ReleaseName) ?? release.Name;
        releaseInfo.VideoType = CleanOptional(data.VideoType);
        releaseInfo.AudioType = CleanOptional(data.AudioType);
        releaseInfo.SizeNumber = data.SizeNumber;
        releaseInfo.SizeUnit = CleanOptional(data.SizeUnit);
        releaseInfo.ReleaseDatabaseUrl = CleanOptional(data.ReleaseDatabaseUrl);

        var metadata = release.Metadata;
        if (metadata is null)
        {
            metadata = new ReleaseMetadata
            {
                MetadataDatabaseClassName = ReleaseMetadata.ManualSource,
            };
            release.Metadata = metadata;
        }

        metadata.MetadataDatabaseClassName = ReleaseMetadata.ManualSource;
        metadata.Title = releaseInfo.ReleaseName;
        metadata.CoverUrl = newCoverUrl;
        metadata.Genre = CleanOptional(data.Genre);
        metadata.Description = CleanOptional(data.Description);

        // Keep the legacy columns populated until their cleanup migration.
        releaseInfo.CoverUrl = metadata.CoverUrl;
        releaseInfo.Genre = metadata.Genre;
        releaseInfo.Description = metadata.Description;

        if (!string.Equals(previousCoverUrl, newCoverUrl, StringComparison.Ordinal))
        {
            RemoveUploadedImages(release.ImageUploadConfigs);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateNfoAsync(
        int releaseId,
        string? fileName,
        string content,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseWithInfoAsync(releaseId, cancellationToken);

        var safeFileName = ReleaseNfoService.GetSafeNfoFileName(
            fileName ?? string.Empty,
            release.Name
        );

        if (release.ReleaseNfo is null)
        {
            release.ReleaseNfo = new ReleaseNfo { FileName = safeFileName, Content = content };
        }
        else
        {
            release.ReleaseNfo.FileName = safeFileName;
            release.ReleaseNfo.Content = content;
        }

        ReleaseExternalIdentifierService.SyncImdbIds(
            release: release,
            source: ExternalIdentifierSource.Nfo,
            values: [content]
        );

        await repository.SaveChangesAsync(cancellationToken);

        if (release.ReleaseFolderPath is null)
        {
            return;
        }

        try
        {
            await ReleaseNfoService.SaveNfoFileAsync(
                releaseFolderPath: release.ReleaseFolderPath,
                fileName: safeFileName,
                releaseName: release.Name,
                content: content,
                overwrite: true,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            logger.LogWarning(
                exception,
                "Failed to write manually edited NFO file for release {ReleaseName}",
                release.Name
            );
        }
    }

    private void RemoveUploadedImages(IReadOnlyList<ImageUploadConfig> imageUploadConfigs)
    {
        var uploadedImages = imageUploadConfigs
            .SelectMany(config => config.ImageUploads)
            .Where(upload => upload.UploadState == UploadState.Completed)
            .ToList();

        foreach (var uploadedImage in uploadedImages)
        {
            repository.Remove(uploadedImage);
        }
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
