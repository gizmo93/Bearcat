using Bearcat.Domain.UseCases.FirstStartSetup.Sources;

namespace Bearcat.Website.Pages.FirstStartSetup;

public class FirstStartSetupLocalFolderSourceFormModel
{
    public string BasePath { get; set; } = string.Empty;

    public string? FolderNamePattern { get; set; }

    public bool ExtractArchivesBeforeReleaseCreation { get; set; }

    public FirstStartExistingFolderHandling ExistingFolderHandling { get; set; } =
        FirstStartExistingFolderHandling.UploadTestFolderAndIgnoreOthers;

    public string? TestFolderPath { get; set; }
}
