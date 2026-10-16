namespace Bearcat.Domain.UseCases.FirstStartSetup.Sources;

public enum FirstStartExistingFolderHandling
{
    UploadTestFolderAndIgnoreOthers = 1,
    IgnoreExisting = 2,
    ImportExisting = 3,
}
