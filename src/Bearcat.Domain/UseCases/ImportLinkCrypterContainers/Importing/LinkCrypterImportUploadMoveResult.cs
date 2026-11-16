using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public sealed record LinkCrypterImportUploadMoveResult(
    LinkCrypterImportUploadToMove UploadToMove,
    Upload? NewUpload,
    LinkCrypterImportUploadNotMovedReason? NotMovedReason
);
