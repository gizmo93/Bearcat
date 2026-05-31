using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Alfafile.Api;
using Bearcat.Hosters.Alfafile.Api.File;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Rapidgator.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Alfafile;

public class Alfafile(IAlfafileApiClient apiClient, ILogger<Alfafile> logger) : IHosterWithFolders
{
    public string Name => "Alfafile";

    public IReadOnlyList<string> ConfigurationKeys =>
        [nameof(AlfafileConfig.Username), nameof(AlfafileConfig.Password)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan UploadStatusPollDelay { get; init; } = TimeSpan.FromSeconds(5);

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<AlfafileConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(start: 1, count: 3))
        {
            try
            {
                return await UploadFileInternalAsync(fileDto, config, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
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
        var config = hosterConfig.As<AlfafileConfig>();
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
        var config = JsonSerializer.Deserialize<AlfafileConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize Alfafile config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new AlfafileConfig
        {
            Username = hosterConfig.GetValueOrDefault("Username") ?? string.Empty,
            Password = hosterConfig.GetValueOrDefault("Password") ?? string.Empty,
        };

        return JsonSerializer.Serialize(config);
    }

    public async Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<AlfafileConfig>();
        var response = await apiClient.GetUserInfoAsync(config, cancellationToken);

        return response.Response?.User.Upload.NbPipes;
    }

    public async Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<AlfafileConfig>();

        return await apiClient.CreateFolderAsync(config, folderName, cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<AlfafileConfig>();

        try
        {
            var response = await apiClient.GetUserInfoAsync(config, cancellationToken);

            return new TryLoginResult(
                IsSuccess: response.Status == (int)HttpStatusCode.OK,
                ErrorMessage: response.Details
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

    private async Task<UploadFileResult> UploadFileInternalAsync(
        FileDto fileDto,
        AlfafileConfig config,
        CancellationToken cancellationToken
    )
    {
        await using var stream = File.OpenRead(fileDto.FullFileName);

        var uploadRequest = await apiClient.RequestUploadFileAsync(
            name: Path.GetFileName(fileDto.FullFileName),
            size: stream.Length,
            hash: await CreateMd5HashAsync(stream, cancellationToken),
            folderId: fileDto.FolderId,
            config: config,
            cancellationToken: cancellationToken
        );

        var requestedUpload = uploadRequest.Response?.Upload;
        var instantUploadFile = requestedUpload?.GetFile();

        if (instantUploadFile is not null && requestedUpload?.State == UploadStates.Done)
        {
            return CreateUploadResult(fileDto, uploadRequest, instantUploadFile.Url);
        }

        if (requestedUpload?.Url is null)
        {
            throw new RetryException(
                uploadRequest.Details ?? requestedUpload?.StateLabel ?? string.Empty
            );
        }

        logger.LogInformation(
            "Uploading file {FileName} to Alfafile with URL {Url}",
            fileDto.FullFileName,
            requestedUpload.Url
        );

        var uploadResult = await apiClient.UploadFileAsync(
            uploadUrl: requestedUpload.Url,
            stream: stream,
            fileName: Path.GetFileName(fileDto.FullFileName),
            cancellationToken: cancellationToken
        );

        var uploadStatus = uploadResult;

        while (
            uploadStatus.Response?.Upload?.State
                is UploadStates.Uploading
                    or UploadStates.Processing
        )
        {
            logger.LogInformation(
                "File upload for file {File} still in progress, state label: {Label}. state: {State}, waiting for it to finish",
                fileDto.FullFileName,
                uploadStatus.Response.Upload.StateLabel,
                uploadStatus.Response.Upload.State
            );

            await Task.Delay(UploadStatusPollDelay, cancellationToken);

            uploadStatus = await apiClient.GetUploadInfoAsync(
                config: config,
                uploadId: uploadStatus.Response.Upload.UploadId,
                cancellationToken: cancellationToken
            );
        }

        return CreateUploadResult(
            fileDto,
            uploadStatus,
            uploadStatus.Response?.Upload?.GetFile()?.Url
        );
    }

    private static UploadFileResult CreateUploadResult(
        FileDto fileDto,
        UploadFileResponse uploadStatus,
        string? fileUrl
    )
    {
        var errors = new[] { uploadStatus.Details, uploadStatus.Response?.Upload?.StateLabel }
            .Where(message =>
                !string.IsNullOrWhiteSpace(message)
                && uploadStatus.Response?.Upload?.State != UploadStates.Done
            )
            .OfType<string>()
            .ToList();

        return new UploadFileResult(
            IsSuccess: uploadStatus.Response?.Upload?.State == UploadStates.Done,
            FileDto: fileDto,
            ErrorMessages: errors,
            FileUrl: fileUrl
        );
    }

    private static async Task<string> CreateMd5HashAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        using var md5 = MD5.Create();
        var hashBytes = await md5.ComputeHashAsync(stream, cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        return Convert.ToHexStringLower(hashBytes);
    }
}
