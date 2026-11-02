namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public enum LinkCrypterImportReleaseAction
{
    CreateRelease = 1,
    AttachToExistingRelease = 2,
    AttachToReleaseOfEarlierContainer = 3,
}
