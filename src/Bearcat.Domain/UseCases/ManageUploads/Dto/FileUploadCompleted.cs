namespace Bearcat.Domain.UseCases.ManageUploads.Dto;

public record FileUploadCompleted(
    int UploadId,
    int ArchiveFileId,
    string FullFileName,
    string? FileUrl,
    bool IsSuccess,
    IReadOnlyList<string> Errors,
    bool WasCanceled = false
);
