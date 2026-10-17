using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public record UploadSourceFile(
    int TransferFileId,
    int? ArchiveFileId,
    string? VideoFileRelativePath,
    string FullFileName,
    string? Md5Hash
)
{
    public bool IsSourceOf(UploadedFile uploadedFile)
    {
        return uploadedFile.ArchiveFileId == ArchiveFileId
            && uploadedFile.VideoFileRelativePath == VideoFileRelativePath;
    }
}
