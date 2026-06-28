namespace Bearcat.Domain.UseCases.ManageReleases;

public enum ArchiveFolderChangeResult
{
    Relocated = 1,
    Reimported = 2,
    ConfirmationRequired = 3,
}
