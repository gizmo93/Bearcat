using Bearcat.Abstractions.Hoster;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public record FileToUpload(
    int UploadId,
    UploadSourceFile SourceFile,
    string? FolderId,
    string HosterClassName,
    IHoster Hoster,
    IHosterConfig HosterConfig
);
