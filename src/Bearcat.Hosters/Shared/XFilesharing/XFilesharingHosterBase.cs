using System.Globalization;
using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Shared.XFilesharing;

public abstract class XFilesharingHosterBase<TConfig>(
    IXFilesharingApiClient apiClient,
    ILogger logger
) : IHosterWithFolders
    where TConfig : IXFilesharingHosterConfig
{
    public abstract string Name { get; }

    public abstract bool SupportsPremiumOnlyDownloads { get; }

    public IReadOnlyList<string> ConfigurationKeys => [nameof(IXFilesharingHosterConfig.ApiKey)];

    protected abstract string FileUrlFormat { get; }

    protected virtual int MaximumParallelUploads => 50;

    protected virtual int UploadRetryAttempts => 3;

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<TConfig>();

        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(start: 1, count: UploadRetryAttempts))
        {
            try
            {
                var response = await UploadFileInternalAsync(
                    fileDto: fileDto,
                    config: config,
                    cancellationToken: cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: true,
                    ErrorMessages: [],
                    FileDto: fileDto,
                    FileUrl: BuildFileUrl(response.FileCode)
                );
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;

                logger.LogError(
                    ex,
                    message: "Upload attempt {Attempt} failed for file {FileName}: {Message}",
                    attempt,
                    fileDto.FullFileName,
                    message
                );

                errors.Add(message);
            }
        }

        return new UploadFileResult(
            IsSuccess: false,
            FileDto: fileDto,
            ErrorMessages: errors,
            FileUrl: null
        );
    }

    public async Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<TConfig>();
        var fileUrls = files.Select(file => file.Url).ToList();

        var fileUrlByFileCode = fileUrls
            .Distinct()
            .Select(url => new { Url = url, FileCode = ExtractFileCode(url) })
            .Where(file => !string.IsNullOrWhiteSpace(file.FileCode))
            .GroupBy(file => file.FileCode!)
            .ToDictionary(group => group.Key, group => group.First().Url);

        try
        {
            var results = await apiClient.FilesExistAsync(
                apiKey: config.ApiKey,
                fileCodes: fileUrlByFileCode.Keys.ToHashSet(),
                cancellationToken: cancellationToken
            );

            var statusPerFileUrl = results
                .Where(kvp => fileUrlByFileCode.ContainsKey(kvp.Key))
                .ToDictionary(kvp => fileUrlByFileCode[kvp.Key], kvp => kvp.Value);

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerFileUrl
            );
        }
        catch (Exception ex)
        {
            return new FileExistResult(
                IsSuccess: false,
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message],
                StatusPerFileUrl: new Dictionary<string, bool>()
            );
        }
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<TConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException(
                $"Failed to deserialize {typeof(TConfig).Name} config"
            );
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        return JsonSerializer.Serialize(hosterConfig);
    }

    public Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<int?>(MaximumParallelUploads);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<TConfig>();

        try
        {
            var result = await apiClient.GetAccountInfoAsync(
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken
            );

            var isSuccess = result.Status is (int)HttpStatusCode.OK;

            return new TryLoginResult(
                IsSuccess: isSuccess,
                ErrorMessage: isSuccess ? null : result.Msg
            );
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<TConfig>();

        return await apiClient.CreateFolderAsync(
            apiKey: config.ApiKey,
            folderName: folderName,
            cancellationToken: cancellationToken
        );
    }

    protected virtual string BuildFileUrl(string fileCode)
    {
        return string.Format(CultureInfo.InvariantCulture, FileUrlFormat, fileCode);
    }

    protected virtual string? ExtractFileCode(string fileUrl)
    {
        var fileCode = fileUrl;

        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            fileCode = uri.AbsolutePath;
        }

        fileCode = fileCode.Trim('/').Split('/').LastOrDefault() ?? string.Empty;

        if (fileCode.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            fileCode = fileCode[..^".html".Length];
        }

        return string.IsNullOrWhiteSpace(fileCode) ? null : fileCode;
    }

    private async Task<UploadFileResponse> UploadFileInternalAsync(
        FileDto fileDto,
        TConfig config,
        CancellationToken cancellationToken
    )
    {
        var uploadRequest = await apiClient.RequestUploadAsync(
            apiKey: config.ApiKey,
            cancellationToken: cancellationToken
        );

        if (!((HttpStatusCode)uploadRequest.Status).IsSuccessStatusCode)
        {
            throw new InvalidOperationException(uploadRequest.Msg);
        }

        if (
            string.IsNullOrWhiteSpace(uploadRequest.UploadUrl)
            || string.IsNullOrWhiteSpace(uploadRequest.SessionId)
        )
        {
            throw new InvalidOperationException("Upload server response is missing upload data");
        }

        await using var stream = File.OpenRead(fileDto.FullFileName);

        var uploadResponse = await apiClient.UploadFileAsync(
            stream: stream,
            uploadUrl: uploadRequest.UploadUrl,
            sessionId: uploadRequest.SessionId,
            fileName: Path.GetFileName(fileDto.FullFileName),
            cancellationToken: cancellationToken
        );

        if (string.IsNullOrWhiteSpace(uploadResponse.FileCode))
        {
            throw new InvalidOperationException("Upload response is missing file code");
        }

        if (!string.IsNullOrWhiteSpace(fileDto.FolderId))
        {
            await apiClient.SetFileFolderAsync(
                apiKey: config.ApiKey,
                fileCode: uploadResponse.FileCode,
                folderId: fileDto.FolderId,
                cancellationToken: cancellationToken
            );
        }

        if (fileDto.PremiumOnlyDownload)
        {
            await apiClient.SetFilePropertiesAsync(
                apiKey: config.ApiKey,
                fileCode: uploadResponse.FileCode,
                premiumOnly: true,
                cancellationToken: cancellationToken
            );
        }

        return uploadResponse;
    }
}
