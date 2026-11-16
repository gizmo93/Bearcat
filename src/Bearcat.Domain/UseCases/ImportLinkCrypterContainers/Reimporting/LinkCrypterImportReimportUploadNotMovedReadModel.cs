using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public sealed record LinkCrypterImportReimportUploadNotMovedReadModel(
    string UploadConfigName,
    string? ArchiveBaseName,
    int FileCount,
    LinkCrypterImportUploadNotMovedReason Reason
);
