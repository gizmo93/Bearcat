using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Fichier.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Fichier;

public class Fichier(IFichierApiClient apiClient, ILogger<Fichier> logger) : IHosterWithFolders
{
    public string Name => "1fichier";

    public bool SupportsPremiumOnlyDownloads => false;

    public IReadOnlyList<string> ConfigurationKeys => [nameof(FichierConfig.ApiKey)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        IUploadProgress progress,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<FichierConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(1, 3))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FileName} to 1fichier (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                await using var stream = File.OpenRead(fileDto.FullFileName);
                var response = await apiClient.UploadFileAsync(
                    config: config,
                    stream: new CountingStream(stream, progress),
                    fileName: Path.GetFileName(fileDto.FullFileName),
                    folderId: fileDto.FolderId,
                    cancellationToken: cancellationToken
                );

                var uploadedLink = response.Links.FirstOrDefault();

                if (uploadedLink is not null)
                {
                    return new UploadFileResult(
                        IsSuccess: true,
                        FileDto: fileDto,
                        ErrorMessages: [],
                        FileUrl: uploadedLink.Download
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Upload attempt {Attempt} failed for file {FileName}: {Message}",
                    attempt,
                    fileDto.FullFileName,
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
        var config = hosterConfig.As<FichierConfig>();
        var fileUrls = files.Select(file => file.Url).ToList();

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
        var config = JsonSerializer.Deserialize<FichierConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize 1fichier config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new FichierConfig
        {
            ApiKey = hosterConfig.GetValueOrDefault(nameof(FichierConfig.ApiKey)) ?? string.Empty,
        };

        return JsonSerializer.Serialize(config);
    }

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => 3;

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
        var config = hosterConfig.As<FichierConfig>();

        return await apiClient.CreateFolderAsync(config, folderName, cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<FichierConfig>();

        try
        {
            var response = await apiClient.GetUserInfoAsync(config, cancellationToken);
            var isSuccess =
                !string.Equals(response.Status, "KO", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(response.Email);

            return new TryLoginResult(
                IsSuccess: isSuccess,
                ErrorMessage: isSuccess ? null : response.Message ?? response.Status
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
