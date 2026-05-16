using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.GoFile.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.GoFile;

public class GoFile(ApiClient apiClient, ILogger<GoFile> logger) : IHoster
{
    public string Name => "GoFile";

    public IReadOnlyList<string> ConfigurationKeys => [nameof(GoFileConfig.ApiKey)];

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<GoFileConfig>();

        var error = string.Empty;

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
                    fileName: Path.GetFileName(fileDto.FullFileName),
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
                    FileUrl: response.Data!.DownloadUrl
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Error while uploading file {FilePath} to GoFile on attempt {Attempt}: {ErrorMessage}",
                    fileDto.FullFileName,
                    attempt,
                    ex.InnerException?.Message ?? ex.Message
                );

                error = ex.InnerException?.Message ?? ex.Message;
            }

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
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
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<GoFileConfig>();

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
}
