using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploads.Dto;

public record FileUploadCompleted(
    Upload Upload,
    ArchiveFile ArchiveFile,
    string? FileUrl,
    bool IsSuccess,
    IReadOnlyList<string> Errors
);
