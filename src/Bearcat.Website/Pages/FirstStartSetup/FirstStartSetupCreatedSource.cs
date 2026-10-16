using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;

namespace Bearcat.Website.Pages.FirstStartSetup;

public sealed record FirstStartSetupCreatedSource(
    FirstStartSetupSourceKind SourceKind,
    string? RemoteSourceAutomationName,
    string FolderPath,
    int? TestReleaseId,
    InitialUploadCreationForReleaseResult? TestReleaseInitialUploadCreationResult
);
