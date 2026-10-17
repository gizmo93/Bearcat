using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Shared.XVideoSharing.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Shared.XVideoSharing;

public abstract class XVideoSharingHosterBase<TConfig>(
    XVideoSharingApiClient apiClient,
    ILogger logger
) : IVideoHoster
    where TConfig : IXVideoSharingHosterConfig
{
    private const int UploadAttempts = 3;

    public abstract string Name { get; }

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(IXVideoSharingHosterConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => MaximumParallelUploads;

    protected virtual int MaximumParallelUploads => 3;

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<TConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(1, UploadAttempts))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to {HosterName} (Attempt {Attempt})",
                    fileDto.FullFileName,
                    Name,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var uploadedFile = await apiClient.UploadFileAsync(
                    apiKey: config.ApiKey,
                    stream: new ProgressReportingStream(stream, progress, stream.Length),
                    fileName: Path.GetFileName(fileDto.FullFileName),
                    cancellationToken: cancellationToken
                );

                if (fileDto.FolderId is not null)
                {
                    await MoveUploadedFileToFolderAsync(
                        apiKey: config.ApiKey,
                        fileCode: uploadedFile.FileCode,
                        folderId: fileDto.FolderId,
                        cancellationToken: cancellationToken
                    );
                }

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: uploadedFile.FileUrl,
                    ExternalId: uploadedFile.FileCode,
                    EmbedUrl: uploadedFile.EmbedUrl
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
                    "Error while uploading file {FilePath} to {HosterName} on attempt {Attempt}: {ErrorMessage}",
                    fileDto.FullFileName,
                    Name,
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
        var config = hosterConfig.As<TConfig>();

        var fileUrlsPerFileCode = files
            .DistinctBy(file => file.Url)
            .Select(file => new
            {
                FileCode = file.ExternalId ?? ExtractFileCode(file.Url),
                file.Url,
            })
            .Where(entry => entry.FileCode is not null)
            .GroupBy(entry => entry.FileCode!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Url).ToList(),
                StringComparer.Ordinal
            );

        try
        {
            var isOnlinePerFileCode = await apiClient.GetOnlineStatusAsync(
                apiKey: config.ApiKey,
                fileCodes: fileUrlsPerFileCode.Keys.ToHashSet(StringComparer.Ordinal),
                cancellationToken: cancellationToken
            );

            var isOnlinePerFileUrl = new Dictionary<string, bool>();

            foreach (var (fileCode, fileUrls) in fileUrlsPerFileCode)
            {
                foreach (var fileUrl in fileUrls)
                {
                    isOnlinePerFileUrl[fileUrl] = isOnlinePerFileCode[fileCode];
                }
            }

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
        return Task.FromResult(DefaultMaximumParallelUploads);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<TConfig>();

        try
        {
            await apiClient.EnsureCredentialsAcceptedAsync(config.ApiKey, cancellationToken);

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

    public async Task MoveFileToFolderAsync(
        string fileUrl,
        string? externalId,
        string folderId,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<TConfig>();
        var fileCode =
            externalId
            ?? ExtractFileCode(fileUrl)
            ?? throw new InvalidOperationException(
                $"Could not extract the {Name} file code from URL {fileUrl}"
            );

        await apiClient.MoveFileToFolderAsync(
            apiKey: config.ApiKey,
            fileCode: fileCode,
            folderId: folderId,
            cancellationToken: cancellationToken
        );
    }

    protected virtual string? ExtractFileCode(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    private async Task MoveUploadedFileToFolderAsync(
        string apiKey,
        string fileCode,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await apiClient.MoveFileToFolderAsync(
                apiKey: apiKey,
                fileCode: fileCode,
                folderId: folderId,
                cancellationToken: cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not HosterCredentialsRejectedException)
        {
            logger.LogWarning(
                ex,
                "Uploaded file {FileCode} could not be moved to {HosterName} folder {FolderId}: {ErrorMessage}",
                fileCode,
                Name,
                folderId,
                ex.InnerException?.Message ?? ex.Message
            );
        }
    }
}
