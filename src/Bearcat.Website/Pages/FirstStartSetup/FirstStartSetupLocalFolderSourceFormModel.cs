namespace Bearcat.Website.Pages.FirstStartSetup;

public class FirstStartSetupLocalFolderSourceFormModel
{
    public string BasePath { get; set; } = string.Empty;

    public string? FolderNamePattern { get; set; }

    public bool ExtractArchivesBeforeReleaseCreation { get; set; }

    public FirstStartSetupExistingFolderHandling ExistingFolderHandling { get; set; } =
        FirstStartSetupExistingFolderHandling.UploadTestFolderAndIgnoreOthers;

    public string? TestFolderPath { get; set; }
}
