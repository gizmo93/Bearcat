using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.CollectionAssignment;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;

public class ReleaseFromFolderCreationService(
    ReleaseInfoResolutionService releaseInfoResolutionService,
    MediaMetadataService mediaMetadataService,
    IArchiverFactory archiverFactory,
    IReleaseCollectionAssigner releaseCollectionAssigner
)
{
    public async Task<Release> CreateAsync(
        ReleaseTemplate releaseTemplate,
        string folderPath,
        string? primaryLanguageCode,
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        var releaseData = ReleaseService.CreateFromTemplateData(
            releaseTemplate: releaseTemplate,
            releaseFolderPath: folderPath,
            name: null,
            releaseType: releaseTemplate.ReleaseType,
            archivers: releaseTemplate.ReleaseType is ReleaseType.Unmanaged
                ? archiverFactory.GetArchivers()
                : [],
            localNow: localNow
        );
        var release = releaseData.Release;

        release.CreatedAt = localNow;
        release.PrimaryLanguageCode = primaryLanguageCode;

        await releaseCollectionAssigner.AssignFromTemplateAsync(
            release: release,
            releaseTemplate: releaseTemplate,
            uploadConfigMatches: releaseData.UploadConfigMatches,
            cancellationToken: cancellationToken
        );

        await releaseInfoResolutionService.TryResolveAsync(release, cancellationToken);

        await mediaMetadataService.TryExtractAsync(release, cancellationToken);

        return release;
    }
}
