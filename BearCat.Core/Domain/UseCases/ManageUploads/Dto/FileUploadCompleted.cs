using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageUploads.Dto;

public record FileUploadCompleted(
    Upload Upload,
    ArchiveFile ArchiveFile,
    string? FileUrl,
    bool IsSuccess,
    IReadOnlyList<string> Errors);
