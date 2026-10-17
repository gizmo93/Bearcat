namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public record FileUploadCompleted(
    int UploadId,
    UploadSourceFile SourceFile,
    string? FileUrl,
    string? ExternalId,
    bool IsSuccess,
    IReadOnlyList<string> Errors,
    string? EmbedUrl = null,
    bool WasCanceled = false
);
