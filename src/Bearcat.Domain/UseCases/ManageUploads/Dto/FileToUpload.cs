using Bearcat.Abstractions.Hoster;

namespace Bearcat.Domain.UseCases.ManageUploads.Dto;

public record FileToUpload(
    int UploadId,
    int ArchiveFileId,
    string FullFileName,
    string HosterClassName,
    IHoster Hoster,
    IHosterConfig HosterConfig
);
