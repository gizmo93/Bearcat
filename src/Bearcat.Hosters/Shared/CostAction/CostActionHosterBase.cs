using System.Text.Json;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Shared.CostAction.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Shared.CostAction;

public abstract class CostActionHosterBase<TConfig>(ICostActionApiClient apiClient, ILogger logger)
    : IHosterWithFolders
    where TConfig : ICostActionHosterConfig
{
    private const int UploadAttempts = 3;

    private const int MaxParallelUploads = 5;

    public abstract string Name { get; }

    protected abstract string AppType { get; }

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(ICostActionHosterConfig.ApiKey)];

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => MaxParallelUploads;

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<TConfig>();
        var errors = new List<string>();
        var fileInfo = new FileInfo(fileDto.FullFileName);

        foreach (var attempt in Enumerable.Range(1, UploadAttempts))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to {Hoster} (Attempt {Attempt})",
                    fileDto.FullFileName,
                    Name,
                    attempt
                );

                await using var stream = SequentialFileReader.OpenRead(fileDto.FullFileName);
                var uploadResult = await apiClient.UploadFileAsync(
                    apiKey: config.ApiKey,
                    appType: AppType,
                    stream: new ProgressReportingStream(stream, progress, stream.Length),
                    fileName: fileInfo.Name,
                    fileSize: fileInfo.Length,
                    folderId: fileDto.FolderId,
                    cancellationToken: cancellationToken
                );

                return new UploadFileResult(
                    IsSuccess: true,
                    FileDto: fileDto,
                    ErrorMessages: [],
                    FileUrl: uploadResult.FileUrl,
                    ExternalId: uploadResult.FileId
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                logger.LogError(
                    ex,
                    "Error while uploading file {FilePath} to {Hoster} on attempt {Attempt}: {ErrorMessage}",
                    fileDto.FullFileName,
                    Name,
                    attempt,
                    message
                );

                errors.Add(message);
            }

            if (attempt < UploadAttempts)
            {
                await Task.Delay(UploadRetryDelay, cancellationToken);
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

        var urlsPerFileId = files
            .DistinctBy(file => file.Url)
            .Select(file => new { FileId = ResolveFileId(file.Url, file.ExternalId), file.Url })
            .Where(entry => entry.FileId is not null)
            .GroupBy(entry => entry.FileId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Url).ToList(),
                StringComparer.Ordinal
            );

        try
        {
            var statusPerFileId = await apiClient.CheckFilesAsync(
                config.ApiKey,
                AppType,
                urlsPerFileId.Keys.ToList(),
                cancellationToken
            );

            var statusPerFileUrl = new Dictionary<string, bool>();
            var downloadCountPerFileUrl = new Dictionary<string, int>();

            foreach (var (fileId, status) in statusPerFileId)
            {
                foreach (var fileUrl in urlsPerFileId.GetValueOrDefault(fileId) ?? [])
                {
                    statusPerFileUrl[fileUrl] = status.IsOnline;

                    if (status.DownloadCount is not null)
                    {
                        downloadCountPerFileUrl[fileUrl] = status.DownloadCount.Value;
                    }
                }
            }

            return new FileExistResult(
                IsSuccess: true,
                ErrorMessages: [],
                StatusPerFileUrl: statusPerFileUrl,
                DownloadCountPerFileUrl: downloadCountPerFileUrl
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
            await apiClient.VerifyAccessAsync(config.ApiKey, AppType, cancellationToken);

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
            config.ApiKey,
            AppType,
            folderName,
            cancellationToken
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
        var fileId =
            ResolveFileId(fileUrl, externalId)
            ?? throw new InvalidOperationException($"Could not extract file id from URL {fileUrl}");

        await apiClient.MoveFileToFolderAsync(config.ApiKey, fileId, folderId, cancellationToken);
    }

    public static string? ExtractFileId(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var fileId = uri
            .AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Replace(".html", string.Empty, StringComparison.OrdinalIgnoreCase);

        return fileId is { Length: > 0 } && fileId.All(char.IsAsciiLetterOrDigit) ? fileId : null;
    }

    private static string? ResolveFileId(string fileUrl, string? externalId)
    {
        return string.IsNullOrWhiteSpace(externalId) ? ExtractFileId(fileUrl) : externalId;
    }
}
