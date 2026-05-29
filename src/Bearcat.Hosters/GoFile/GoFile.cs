using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.GoFile.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.GoFile;

public partial class GoFile(IGoFileApiClient apiClient, ILogger<GoFile> logger) : IHoster
{
    public string Name => "GoFile";

    public IReadOnlyList<string> ConfigurationKeys => [nameof(GoFileConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, string> uploadFolderIds = new();

    private readonly ConcurrentDictionary<string, SemaphoreSlim> uploadFolderLocks = new();

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<GoFileConfig>();

        var error = string.Empty;
        var fileName = Path.GetFileName(fileDto.FullFileName);
        var uploadFolderName = GetUploadFolderName(fileName);
        var uploadFolderKey = GetUploadFolderKey(fileDto);

        foreach (var attempt in Enumerable.Range(1, 3))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to GoFile (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                var folderId = await GetOrCreateUploadFolderIdAsync(
                    apiKey: config.ApiKey,
                    folderKey: uploadFolderKey,
                    folderName: uploadFolderName,
                    cancellationToken: cancellationToken
                );

                await using var stream = File.OpenRead(fileDto.FullFileName);
                var response = await apiClient.UploadFileAsync(
                    apiKey: config.ApiKey,
                    fileStream: stream,
                    fileName: fileName,
                    folderId: folderId,
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

    private async Task<string> GetOrCreateUploadFolderIdAsync(
        string apiKey,
        string folderKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var cacheKey = $"{apiKey}|{folderKey}";

        if (uploadFolderIds.TryGetValue(cacheKey, out var cachedFolderId))
        {
            return cachedFolderId;
        }

        var folderLock = uploadFolderLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        await folderLock.WaitAsync(cancellationToken);

        try
        {
            if (uploadFolderIds.TryGetValue(cacheKey, out cachedFolderId))
            {
                return cachedFolderId;
            }

            var folderId = await apiClient.CreateUploadFolderIdAsync(
                apiKey: apiKey,
                folderName: folderName,
                cancellationToken: cancellationToken
            );

            uploadFolderIds[cacheKey] = folderId;

            return folderId;
        }
        finally
        {
            folderLock.Release();
        }
    }

    private static string GetUploadFolderKey(FileDto fileDto)
    {
        return fileDto.UploadId.ToString();
    }

    private static string GetUploadFolderName(string fileName)
    {
        var rarMatch = RarPartFileNameRegex().Match(fileName);

        if (rarMatch.Success)
        {
            return rarMatch.Groups["base"].Value;
        }

        var sevenZipMatch = SevenZipPartFileNameRegex().Match(fileName);

        return sevenZipMatch.Success
            ? sevenZipMatch.Groups["base"].Value
            : Path.GetFileNameWithoutExtension(fileName);
    }

    [GeneratedRegex(@"^(?<base>.+)\.part\d+\.rar$", RegexOptions.IgnoreCase)]
    private static partial Regex RarPartFileNameRegex();

    [GeneratedRegex(@"^(?<base>.+)\.7z\.\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex SevenZipPartFileNameRegex();

    private static string GetFileUrl(Api.UploadFile.Data data)
    {
        return string.IsNullOrWhiteSpace(data.Id)
            ? data.DownloadUrl
            : $"https://gofile.io/d/{data.Id}";
    }
}
