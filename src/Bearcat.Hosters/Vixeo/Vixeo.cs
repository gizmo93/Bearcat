using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Vixeo.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Vixeo;

public class Vixeo(ApiClient apiClient, ILogger<Vixeo> logger) : IVideoHoster
{
    private const int MaxParallelUploads = 3;

    private const int UploadAttempts = 3;

    public string Name => "Vixeo";

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(VixeoConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => MaxParallelUploads;

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<VixeoConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(1, UploadAttempts))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to Vixeo (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var uploadedVideo = await apiClient.UploadFileAsync(
                    config: config,
                    stream: new ProgressReportingStream(stream, progress, stream.Length),
                    fileName: Path.GetFileName(fileDto.FullFileName),
                    cancellationToken: cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: uploadedVideo.Url,
                    ExternalId: uploadedVideo.Id,
                    EmbedUrl: uploadedVideo.Url
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is not HosterCredentialsRejectedException)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                logger.LogError(
                    ex,
                    "Error while uploading file {FilePath} to Vixeo on attempt {Attempt}: {ErrorMessage}",
                    fileDto.FullFileName,
                    attempt,
                    message
                );

                errors.Add(message);
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
        var config = hosterConfig.As<VixeoConfig>();

        try
        {
            var isOnlinePerFileUrl = await apiClient.CheckLinksAsync(
                config: config,
                files: files,
                cancellationToken: cancellationToken
            );

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: isOnlinePerFileUrl
            );
        }
        catch (Exception ex) when (ex is not HosterCredentialsRejectedException)
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
        var config = JsonSerializer.Deserialize<VixeoConfig>(serializedConfig);

        return config ?? throw new InvalidOperationException("Failed to deserialize Vixeo config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new VixeoConfig
        {
            ApiKey = hosterConfig.GetValueOrDefault(nameof(VixeoConfig.ApiKey)) ?? string.Empty,
        };

        return JsonSerializer.Serialize(config);
    }

    public Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(DefaultMaximumParallelUploads);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<VixeoConfig>();

        try
        {
            await apiClient.EnsureCredentialsAcceptedAsync(config, cancellationToken);

            return new TryLoginResult(IsSuccess: true, ErrorMessage: null);
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
