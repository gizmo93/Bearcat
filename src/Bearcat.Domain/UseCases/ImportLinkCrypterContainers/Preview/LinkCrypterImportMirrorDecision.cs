namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public enum LinkCrypterImportMirrorDecision
{
    Adopt = 1,
    UploadByBearcat = 2,
    UnsupportedHoster = 3,
    NotInReleaseTemplate = 4,
}
