using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseInfoService(IReleaseInfoRepository repository)
{
    private const string ManualSource = "Manual";

    public async Task DeleteAsync(int releaseInfoId, CancellationToken cancellationToken = default)
    {
        var releaseInfo = await repository.GetReleaseInfoByIdAsync(
            releaseInfoId,
            cancellationToken
        );
        repository.Remove(releaseInfo);

        releaseInfo.Release.ReleaseInfoCheckedAt = null;

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
        var previousCoverUrl = release.ReleaseInfo?.CoverUrl;

        var releaseInfo = release.ReleaseInfo;
        if (releaseInfo is null)
        {
            releaseInfo = new ReleaseInfo { NfoDatabaseClassName = ManualSource };
            release.ReleaseInfo = releaseInfo;
        }

        releaseInfo.ReleaseName = CleanOptional(data.ReleaseName) ?? release.Name;
        releaseInfo.CoverUrl = newCoverUrl;
        releaseInfo.Genre = CleanOptional(data.Genre);
        releaseInfo.VideoType = CleanOptional(data.VideoType);
        releaseInfo.AudioType = CleanOptional(data.AudioType);
        releaseInfo.SizeNumber = data.SizeNumber;
        releaseInfo.SizeUnit = CleanOptional(data.SizeUnit);
        releaseInfo.ReleaseDatabaseUrl = CleanOptional(data.ReleaseDatabaseUrl);
        releaseInfo.Description = CleanOptional(data.Description);

        if (!string.Equals(previousCoverUrl, newCoverUrl, StringComparison.Ordinal))
        {
            RemoveUploadedImages(release.ImageUploadConfigs);
        }

        await repository.SaveChangesAsync(cancellationToken);
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
