using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Hosters.Shared;

public static class FileInfoResultMapping
{
    public static Dictionary<string, string> GetFileIdPerFileLink(
        IReadOnlyList<string> fileLinks,
        Func<string, string?> extractFileId
    )
    {
        return fileLinks
            .Distinct()
            .Select(fileLink => new { FileLink = fileLink, FileId = extractFileId(fileLink) })
            .Where(entry => entry.FileId is not null)
            .ToDictionary(entry => entry.FileLink, entry => entry.FileId!);
    }

    public static FileInfoResult CreateResult(
        IReadOnlyDictionary<string, string> fileIdPerFileLink,
        IReadOnlyDictionary<string, HostedFileInfo> fileInfoPerFileId
    )
    {
        var fileInfoPerFileLink = new Dictionary<string, HostedFileInfo>();

        foreach (var (fileLink, fileId) in fileIdPerFileLink)
        {
            if (fileInfoPerFileId.TryGetValue(fileId, out var fileInfo))
            {
                fileInfoPerFileLink[fileLink] = fileInfo;
            }
        }

        return new FileInfoResult(
            IsSuccess: true,
            ErrorMessages: [],
            FileInfoPerFileLink: fileInfoPerFileLink
        );
    }

    public static FileInfoResult CreateFailedResult(Exception exception)
    {
        return new FileInfoResult(
            IsSuccess: false,
            ErrorMessages: [exception.InnerException?.Message ?? exception.Message],
            FileInfoPerFileLink: new Dictionary<string, HostedFileInfo>()
        );
    }
}
