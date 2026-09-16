namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record RemoteConversionPreview(
    bool CanConvert,
    IReadOnlyList<string> DeletableArchiveFolderPaths,
    IReadOnlyList<string> MirrorHosterNames
);
