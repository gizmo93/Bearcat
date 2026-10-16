using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;

namespace Bearcat.Domain.UseCases.FirstStartSetup.Sources;

public class FirstStartFolderSourceService(
    ArchiveOutputFolderInsideSourceFolderValidator archiveOutputFolderValidator,
    ReleaseService releaseService,
    ReleaseFolderAutomationService releaseFolderAutomationService,
    UploadCreationService uploadCreationService
)
{
    public async Task<FirstStartFolderSourceResult> CreateFolderSourceAsync(
        FirstStartFolderSourceInput input,
        CancellationToken cancellationToken = default
    )
    {
        await archiveOutputFolderValidator.EnsureNoArchiveOutputFolderIsInsideSourceFolderAsync(
            input.ReleaseTemplateId,
            input.BasePath,
            cancellationToken
        );

        int? testReleaseId = null;

        if (
            input.ExistingFolderHandling
            is FirstStartExistingFolderHandling.UploadTestFolderAndIgnoreOthers
        )
        {
            testReleaseId = await releaseService.CreateFromTemplateAsync(
                input.ReleaseTemplateId,
                input.TestFolderPath!,
                name: null,
                primaryLanguageCode: null,
                cancellationToken
            );
        }

        var releaseFolderAutomationId = await releaseFolderAutomationService.CreateAsync(
            input.BasePath,
            input.FolderNamePattern,
            input.ReleaseTemplateId,
            primaryLanguageCode: null,
            input.ExtractArchivesBeforeReleaseCreation,
            isEnabled: true,
            ignoreExistingOnFirstScan: input.ExistingFolderHandling
                is not FirstStartExistingFolderHandling.ImportExisting,
            cancellationToken
        );

        if (testReleaseId is not { } releaseId)
        {
            return new FirstStartFolderSourceResult(
                releaseFolderAutomationId,
                TestReleaseId: null,
                TestReleaseInitialUploadCreationResult: null
            );
        }

        var initialUploadCreationResult =
            await uploadCreationService.CreateMissingInitialUploadsForReleaseIgnoringCooldownAsync(
                releaseId,
                cancellationToken
            );

        return new FirstStartFolderSourceResult(
            releaseFolderAutomationId,
            releaseId,
            initialUploadCreationResult
        );
    }
}
