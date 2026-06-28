namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record UnmanagedConversionPreview(
    string? ReleaseFolderPath,
    bool CanConvert,
    bool ArchivesInsideReleaseFolder
);
