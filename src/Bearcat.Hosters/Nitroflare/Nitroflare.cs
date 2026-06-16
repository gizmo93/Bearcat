using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Nitroflare.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Nitroflare;

public class Nitroflare(INitroflareApiClient apiClient, ILogger<Nitroflare> logger) : IHoster
{
    public string Name => "Nitroflare";

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(NitroflareConfig.UserHash)];

    private TimeSpan UploadRetryDelay { get; } = TimeSpan.FromSeconds(30);

    private const int MaxNumberOfParallelUploads = 5;

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        IUploadProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<NitroflareConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(start: 1, count: 5))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to Nitroflare (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = File.OpenRead(fileDto.FullFileName);
                var response = await apiClient.UploadFileAsync(
                    config: config,
                    fileStream: new CountingStream(stream, progress),
                    fileName: Path.GetFileName(fileDto.FullFileName),
                    cancellationToken: cancellationToken
                );

                var uploadedFile = response.Files?.FirstOrDefault();

                return new UploadFileResult(
                    IsSuccess: !string.IsNullOrWhiteSpace(uploadedFile?.Url),
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: uploadedFile?.Url
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error while uploading file {FilePath} to Nitroflare on attempt {Attempt}: {ErrorMessage}",
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
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var fileUrls = files.Select(file => file.Url).ToList();

        try
        {
            var statusPerFileUrl = await apiClient.CheckLinksAsync(fileUrls, cancellationToken);

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
        var config = JsonSerializer.Deserialize<NitroflareConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize Nitroflare config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new NitroflareConfig
        {
            UserHash =
                hosterConfig.GetValueOrDefault(nameof(NitroflareConfig.UserHash)) ?? string.Empty,
        };

        return JsonSerializer.Serialize(config);
    }

    public Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<int?>(MaxNumberOfParallelUploads);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<NitroflareConfig>();

        try
        {
            var response = await apiClient.TestUserHashAsync(config, cancellationToken);
            var uploadedFile = response.Files?.FirstOrDefault();
            var success = !string.IsNullOrWhiteSpace(uploadedFile?.Url);

            return new TryLoginResult(
                IsSuccess: success,
                ErrorMessage: success ? null : uploadedFile?.Error ?? "No upload URL returned"
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
