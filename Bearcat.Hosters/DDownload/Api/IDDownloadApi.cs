using Bearcat.Hosters.DDownload.Api.FileOnlineCheck;
using Refit;

namespace Bearcat.Hosters.DDownload.Api;

public interface IDDownloadApi
{
    [Get("/file/check")]
    Task<Response> CheckFilesExistAsync(
        [Query][AliasAs("key")] string apiKey,
        [Query][AliasAs("file_code")] IReadOnlySet<string> fileCodes,
        CancellationToken cancellationToken);

    [Get("/account/info")]
    Task<AccountInfo.Response> GetAccountInfoAsync(
        [Query][AliasAs("key")] string apiKey,
        CancellationToken cancellationToken);

    [Get("/file/exists")]
    Task<FileExists.Response> FileExistsAsync(
        [Query][AliasAs("key")] string apiKey,
        [Query][AliasAs("name")] string fileName,
        CancellationToken cancellationToken);

    [Get("/file/info")]
    Task<FileInfo.Response> GetFileInfoAsync(
        [Query][AliasAs("key")] string apiKey,
        [Query][AliasAs("file_code")] string fileCode,
        CancellationToken cancellationToken);

    [Get("/upload/server")]
    Task<ApiResponse<RequestUpload.Response>> RequestUploadAsync(
        [Query][AliasAs("key")] string apiKey,
        CancellationToken cancellationToken);
}
