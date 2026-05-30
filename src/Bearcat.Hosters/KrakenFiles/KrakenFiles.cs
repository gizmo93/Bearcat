using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.KrakenFiles.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.KrakenFiles;

public class KrakenFiles(IKrakenFilesApiClient apiClient, ILogger<KrakenFiles> logger)
    : IHosterWithFolders
{
    private const int MaxParallelUploads = 10;

    public string Name => "KrakenFiles";

    public IReadOnlyList<string> ConfigurationKeys => [nameof(KrakenFilesConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<KrakenFilesConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(1, 5))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to KrakenFiles (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = File.OpenRead(fileDto.FullFileName);
                var response = await apiClient.UploadFileAsync(
                    config,
                    stream,
                    Path.GetFileName(fileDto.FullFileName),
                    fileDto.FolderId,
                    cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: response.Status == (int)HttpStatusCode.OK
                        && !string.IsNullOrWhiteSpace(response.Data?.Url),
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: response.Data?.Url
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Error while uploading file {FilePath} to KrakenFiles on attempt {Attempt}: {ErrorMessage}",
                    fileDto.FullFileName,
                    attempt,
                    ex.InnerException?.Message ?? ex.Message
                );

                errors.Add(ex.InnerException?.Message ?? ex.Message);
            }

            await Task.Delay(UploadRetryDelay, cancellationToken);
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
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<KrakenFilesConfig>();

        try
        {
            var statusPerFileUrl = await apiClient.CheckLinksAsync(
                config,
                fileUrls,
                cancellationToken
            );

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
        var config = JsonSerializer.Deserialize<KrakenFilesConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize KrakenFiles config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new KrakenFilesConfig
        {
            ApiKey =
                hosterConfig.GetValueOrDefault(nameof(KrakenFilesConfig.ApiKey)) ?? string.Empty,
        };

        return JsonSerializer.Serialize(config);
    }

    public Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<int?>(MaxParallelUploads);
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<KrakenFilesConfig>();

        return await apiClient.CreateFolderAsync(config, folderName, cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<KrakenFilesConfig>();

        try
        {
            var success = await apiClient.IsApiKeyValidAsync(config, cancellationToken);

            return new TryLoginResult(
                IsSuccess: success,
                ErrorMessage: success ? null : "Invalid credentials"
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
