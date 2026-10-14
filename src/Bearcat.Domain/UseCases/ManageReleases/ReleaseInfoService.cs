using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseInfoService(
    IReleaseInfoRepository repository,
    ReleaseClassificationService classificationService,
    ILogger<ReleaseInfoService> logger
)
{
    public async Task DeleteAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        var release = await repository.GetReleaseWithInfoAsync(releaseId, cancellationToken);

        if (release.ReleaseInfo is not null)
        {
            repository.Remove(release.ReleaseInfo);
        }

        if (release.Metadata is not null)
        {
            repository.Remove(release.Metadata);
        }

        release.ReleaseInfoCheckedAt = null;
        release.MetadataCheckedAt = null;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateReleaseInfoAsync(
        int releaseId,
        EditReleaseInfoData data,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseForCoverUpdateAsync(releaseId, cancellationToken);
        var newCoverUrl = data.CoverUrl.TrimOrNullWhenWhitespace();
        var previousCoverUrl = release.Metadata?.CoverUrl;

        var releaseInfo = release.ReleaseInfo;
        if (releaseInfo is null)
        {
            releaseInfo = ReleaseInfo.CreatePlaceholder(ReleaseInfo.ManualSource, release.Name);
            release.ReleaseInfo = releaseInfo;
        }

        releaseInfo.ReleaseName = data.ReleaseName.TrimOrNullWhenWhitespace() ?? release.Name;
        releaseInfo.VideoType = data.VideoType.TrimOrNullWhenWhitespace();
        releaseInfo.AudioType = data.AudioType.TrimOrNullWhenWhitespace();
        releaseInfo.SizeNumber = data.SizeNumber;
        releaseInfo.SizeUnit = data.SizeUnit.TrimOrNullWhenWhitespace();
        releaseInfo.ReleaseDatabaseUrl = data.ReleaseDatabaseUrl.TrimOrNullWhenWhitespace();

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
        metadata.Genre = data.Genre.TrimOrNullWhenWhitespace();
        metadata.Description = data.Description.TrimOrNullWhenWhitespace();

        ReleaseExternalIdentifierService.SyncImdbIds(
            release: release,
            source: ExternalIdentifierSource.Manual,
            values: [data.ImdbId]
        );

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
        ReleaseExternalIdentifierService.SyncSteamAppIds(
            release: release,
            source: ExternalIdentifierSource.Nfo,
            values: [content]
        );

        classificationService.Classify(release);

        release.MetadataCheckedAt = null;

        if (release.ReleaseCollection is not null)
        {
            release.ReleaseCollection.MetadataCheckedAt = null;
        }

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
}
