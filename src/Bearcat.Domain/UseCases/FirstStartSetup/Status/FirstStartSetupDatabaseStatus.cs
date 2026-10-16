namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public sealed record FirstStartSetupDatabaseStatus(
    bool HasHosterRegistration,
    bool HasReleaseTemplate,
    bool HasReleaseFolderAutomationOrRemoteSourceAutomation,
    bool HasRelease
);
