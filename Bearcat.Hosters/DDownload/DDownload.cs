using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.DDownload.Api;
using Bearcat.Hosters.DDownload.Api.UploadFile;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Rapidgator.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.DDownload;

public class DDownload(
    ApiClient apiClient,
    ILogger<DDownload> logger)
    : IHoster
{
    public string Name => "ddownload";

    public IReadOnlyList<string> ConfigurationKeys => [nameof(DDownloadConfig.ApiKey)];

    private const string DdownloadBaseUrl = "https://www.ddownload.com";


    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        var config = hosterConfig.As<DDownloadConfig>();

        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(start: 1, count: 3))
        {
            try
            {
                var response = await UploadFileInternalAsync(
                    fileDto: fileDto,
                    config: config,
                    cancellationToken: cancellationToken);

                return new UploadFileResult(
                    IsSuccess: true,
                    ErrorMessages: [],
                    FileDto: fileDto,
                    FileUrl: $"{DdownloadBaseUrl}/{response.FileCode}");

            }
            catch (Exception ex)
            {
                logger.LogError(message: "Upload attempt {Attempt} failed for file {FileName}: {Message}",
                    attempt,
                    fileDto.FullFileName,
                    ex.InnerException?.Message ?? ex.Message);

                errors.Add(ex.Message);
            }
        }

        return new UploadFileResult(
            IsSuccess: false,
            FileDto: fileDto,
            ErrorMessages: errors,
            FileUrl: null);
    }

    public async Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken)
    {
        var config = hosterConfig.As<DDownloadConfig>();

        var fileUrlByFileCode = fileUrls
            .Distinct()
            .ToDictionary(url => url.Split('/').Last(), url => url);

        try
        {
            var results = await apiClient.FilesExistAsync(
                apiKey: config.ApiKey,
                fileCodes: fileUrlByFileCode.Keys.ToHashSet(),
                cancellationToken: cancellationToken);

            var statusPerFileUrl = results
                .ToDictionary(kvp => fileUrlByFileCode[kvp.Key], kvp => kvp.Value);

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerFileUrl);
        }
        catch (Exception ex)
        {
            return new FileExistResult(
                IsSuccess: false,
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message],
                StatusPerFileUrl: new Dictionary<string, bool>());
        }
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<DDownloadConfig>(serializedConfig)!;
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        return JsonSerializer.Serialize(hosterConfig);
    }

    public Task<int?> GetMaximumParallelUploadsAsync(IHosterConfig hosterConfig, CancellationToken cancellationToken)
    {
        return Task.FromResult<int?>(50);
    }

    public async Task<TryLoginResult> TryLoginAsync(IHosterConfig hosterConfig, CancellationToken cancellationToken)
    {
        var config = hosterConfig.As<DDownloadConfig>();

        try
        {
            var result = await apiClient.GetAccountInfoAsync(
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken);

            return new TryLoginResult(
                IsSuccess: result.Status is (int)HttpStatusCode.OK,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message);
        }
    }
    
    private async Task<Response> UploadFileInternalAsync(
        FileDto fileDto,
        DDownloadConfig config,
        CancellationToken cancellationToken)
    {
        var uploadRequest = await apiClient.RequestUploadAsync(
            apiKey: config.ApiKey,
            cancellationToken: cancellationToken);

        if (!((HttpStatusCode)uploadRequest.Status).IsSuccessStatusCode)
        {
            throw new RetryException(uploadRequest.Msg);
        }

        await using var stream = File.OpenRead(fileDto.FullFileName);

        var uploadResponse = await apiClient.UploadFileAsync(
            stream: stream,
            uploadUrl: uploadRequest.UploadUrl!,
            sessionId: uploadRequest.SessionId!,
            fileName: Path.GetFileName(fileDto.FullFileName),
            cancellationToken: cancellationToken);

        return uploadResponse;
    }
}
