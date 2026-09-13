using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.CloudFam.Api;

public interface ICloudFamApiClient
{
    Task<CloudFamUploadResult> UploadFileAsync(
        CloudFamConfig config,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        CloudFamConfig config,
        string folderName,
        CancellationToken cancellationToken
    );

    Task MoveFileToFolderAsync(
        CloudFamConfig config,
        string fileUrl,
        string? externalId,
        string folderId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, LinkCheckStatus>> CheckLinksAsync(
        CloudFamConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    );

    Task<bool> IsApiKeyValidAsync(CloudFamConfig config, CancellationToken cancellationToken);
}

public record CloudFamUploadResult(string FileId, string FileUrl);
