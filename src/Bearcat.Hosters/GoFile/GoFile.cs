using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.GoFile.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.GoFile;

public class GoFile(IGoFileApiClient apiClient, ILogger<GoFile> logger) : IHosterWithFolders
{
    public string Name => "GoFile";

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(GoFileConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<GoFileConfig>();

        var error = string.Empty;
        var fileName = Path.GetFileName(fileDto.FullFileName);

        if (string.IsNullOrWhiteSpace(fileDto.FolderId))
        {
            return new UploadFileResult(
                IsSuccess: false,
                FileDto: fileDto,
                ErrorMessages: ["GoFile upload requires a folder id"],
                FileUrl: null
            );
        }

        foreach (var attempt in Enumerable.Range(1, 3))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to GoFile (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = File.OpenRead(fileDto.FullFileName);
                var response = await apiClient.UploadFileAsync(
                    apiKey: config.ApiKey,
                    fileStream: stream,
                    fileName: fileName,
                    folderId: fileDto.FolderId,
                    cancellationToken: cancellationToken
                );

                var success = response.Status == "ok";

                if (!success)
                {
                    continue;
                }

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: new List<string> { }.AsReadOnly(),
                    FileUrl: GetFileUrl(response.Data!)
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error while uploading file {FilePath} to GoFile on attempt {Attempt}: {ErrorMessage}",
                    fileDto.FullFileName,
                    attempt,
                    ex.InnerException?.Message ?? ex.Message
                );

                error = ex.InnerException?.Message ?? ex.Message;
            }

            await Task.Delay(UploadRetryDelay, cancellationToken);
        }

        return new UploadFileResult(
            IsSuccess: false,
            FileDto: fileDto,
            ErrorMessages: [error],
            FileUrl: null
        );
    }

    public async Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<GoFileConfig>();
        var fileUrls = files.Select(file => file.Url).ToList();

        var result = await apiClient.CheckOnlineStatusAsync(
            fileUrls: fileUrls,
            apiKey: config.ApiKey,
            cancellationToken: cancellationToken
        );

        var errorMessages = result
            .Values.Select(v => v.ErrorMessage)
            .Where(e => e is not null)
            .OfType<string>()
            .ToList();

        var linkStatuses = result.ToDictionary(r => r.Key, r => r.Value.IsOnline);

        var isSuccess = result.Values.All(v => v.ErrorMessage is null);

        return new FileExistResult(
            IsSuccess: isSuccess,
            ErrorMessages: errorMessages,
            StatusPerFileUrl: linkStatuses
        );
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<GoFileConfig>();

        return await apiClient.CreateUploadFolderIdAsync(
            apiKey: config.ApiKey,
            folderName: folderName,
            cancellationToken: cancellationToken
        );
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<GoFileConfig>(serializedConfig)!;
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
        return Task.FromResult<int?>(100);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<GoFileConfig>();

        try
        {
            var response = await apiClient.GetAccountAsync(config.ApiKey, cancellationToken);

            var success = response.Status == "ok";

            return new TryLoginResult(
                IsSuccess: success,
                ErrorMessage: success ? null : response.Status
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

    private static string GetFileUrl(Api.UploadFile.Data data)
    {
        return string.IsNullOrWhiteSpace(data.Id)
            ? data.DownloadUrl
            : $"https://gofile.io/d/{data.Id}";
    }
}
