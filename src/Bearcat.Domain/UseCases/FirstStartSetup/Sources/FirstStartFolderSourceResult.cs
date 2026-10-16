using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;

namespace Bearcat.Domain.UseCases.FirstStartSetup.Sources;

public sealed record FirstStartFolderSourceResult(
    int ReleaseFolderAutomationId,
    int? TestReleaseId,
    InitialUploadCreationForReleaseResult? TestReleaseInitialUploadCreationResult
);
