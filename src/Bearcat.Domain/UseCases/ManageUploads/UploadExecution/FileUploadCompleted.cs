namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public record FileUploadCompleted(
    int UploadId,
    int ArchiveFileId,
    string FullFileName,
    string? FileUrl,
    string? ExternalId,
    bool IsSuccess,
    IReadOnlyList<string> Errors,
    string? Md5Hash = null,
    bool WasCanceled = false
);
