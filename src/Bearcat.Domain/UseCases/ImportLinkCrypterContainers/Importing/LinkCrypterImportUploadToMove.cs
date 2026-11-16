using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public sealed record LinkCrypterImportUploadToMove(
    string ReleaseName,
    Upload OriginalUpload,
    IReadOnlyList<UploadedFile> UploadedFiles
);
