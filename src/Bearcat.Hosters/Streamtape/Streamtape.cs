using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Streamtape.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Streamtape;

public class Streamtape(ApiClient apiClient, ILogger<Streamtape> logger)
    : IVideoHoster,
        IHosterWithFolders
{
    private const int MaxParallelUploads = 3;

    private const int UploadAttempts = 3;

    private const string EmbedBaseUrl = "https://streamtape.com/e/";

    public string Name => "Streamtape";

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys =>
        [nameof(StreamtapeConfig.ApiLogin), nameof(StreamtapeConfig.ApiKey)];

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
        var config = hosterConfig.As<StreamtapeConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(1, UploadAttempts))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to Streamtape (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var uploadedFile = await apiClient.UploadFileAsync(
                    config: config,
                    stream: new ProgressReportingStream(stream, progress, stream.Length),
                    fileName: Path.GetFileName(fileDto.FullFileName),
                    folderId: fileDto.FolderId,
                    cancellationToken: cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: uploadedFile.FileUrl,
                    ExternalId: uploadedFile.FileId,
                    EmbedUrl: EmbedBaseUrl + uploadedFile.FileId
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
                    "Error while uploading file {FilePath} to Streamtape on attempt {Attempt}: {ErrorMessage}",
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
        var config = hosterConfig.As<StreamtapeConfig>();

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
        var config = JsonSerializer.Deserialize<StreamtapeConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize Streamtape config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new StreamtapeConfig
        {
            ApiLogin =
                hosterConfig.GetValueOrDefault(nameof(StreamtapeConfig.ApiLogin)) ?? string.Empty,
            ApiKey =
                hosterConfig.GetValueOrDefault(nameof(StreamtapeConfig.ApiKey)) ?? string.Empty,
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

    public async Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<StreamtapeConfig>();

        return await apiClient.CreateFolderAsync(config, folderName, cancellationToken);
    }

    public async Task MoveFileToFolderAsync(
        string fileUrl,
        string? externalId,
        string folderId,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<StreamtapeConfig>();

        await apiClient.MoveFileToFolderAsync(
            config: config,
            fileUrl: fileUrl,
            externalId: externalId,
            folderId: folderId,
            cancellationToken: cancellationToken
        );
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<StreamtapeConfig>();

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
