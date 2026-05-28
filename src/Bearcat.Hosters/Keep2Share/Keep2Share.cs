using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Extensions;
using Bearcat.Hosters.Keep2Share.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Keep2Share;

public class Keep2Share(IKeep2ShareApiClient apiClient, ILogger<Keep2Share> logger)
    : IHoster,
        ISupportCaptchaVerification
{
    private const int MaxParallelUploads = 10;

    public string Name => "Keep2Share";

    public IReadOnlyList<string> ConfigurationKeys =>
        [nameof(Keep2ShareConfig.EmailAddress), nameof(Keep2ShareConfig.Password)];

    public TimeSpan UploadRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<Keep2ShareConfig>();
        var errors = new List<string>();

        foreach (var attempt in Enumerable.Range(start: 1, count: 3))
        {
            try
            {
                logger.LogInformation(
                    "Uploading file {FilePath} to Keep2Share (Attempt {Attempt})",
                    fileDto.FullFileName,
                    attempt
                );

                var response = await UploadFileInternalAsync(fileDto, config, cancellationToken);

                var success =
                    response.Success == true
                    || response.Status == "success"
                    || response.StatusCode is (int)HttpStatusCode.OK;

                if (success && !string.IsNullOrWhiteSpace(response.Link))
                {
                    return new UploadFileResult(
                        IsSuccess: true,
                        FileDto: fileDto,
                        ErrorMessages: [],
                        FileUrl: response.Link
                    );
                }

                errors.Add(response.Message ?? $"Upload failed with status {response.Status}");
            }
            catch (Exception ex)
            {
                if (ex is CaptchaVerificationRequiredException)
                {
                    throw;
                }

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
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<Keep2ShareConfig>();

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
            if (ex is CaptchaVerificationRequiredException)
            {
                throw;
            }

            return new FileExistResult(
                IsSuccess: false,
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message],
                StatusPerFileUrl: new Dictionary<string, bool>()
            );
        }
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<Keep2ShareConfig>(serializedConfig);

        return config
            ?? throw new InvalidOperationException("Failed to deserialize Keep2Share config");
    }

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig)
    {
        var config = new Keep2ShareConfig
        {
            EmailAddress = hosterConfig[nameof(Keep2ShareConfig.EmailAddress)],
            Password = hosterConfig[nameof(Keep2ShareConfig.Password)],
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

    public async Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<Keep2ShareConfig>();

        try
        {
            var response = await apiClient.LoginAsync(config, cancellationToken);

            return new TryLoginResult(
                IsSuccess: response.Status == "success" && response.Code == (int)HttpStatusCode.OK,
                ErrorMessage: response.Status == "success" ? null : response.Message
            );
        }
        catch (Exception ex)
        {
            if (ex is CaptchaVerificationRequiredException)
            {
                throw;
            }

            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    public async Task<CaptchaChallengeResult> RequestCaptchaChallengeAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        return await apiClient.RequestCaptchaChallengeAsync(cancellationToken);
    }

    public async Task<TryLoginResult> VerifyCaptchaAsync(
        IHosterConfig hosterConfig,
        string challenge,
        string response,
        CancellationToken cancellationToken
    )
    {
        var config = hosterConfig.As<Keep2ShareConfig>();
        return await apiClient.VerifyCaptchaAsync(
            config: config,
            challenge: challenge,
            response: response,
            cancellationToken: cancellationToken
        );
    }

    private async Task<UploadFileResponse> UploadFileInternalAsync(
        FileDto fileDto,
        Keep2ShareConfig config,
        CancellationToken cancellationToken
    )
    {
        var uploadFormData = await apiClient.RequestUploadAsync(config, cancellationToken);

        await using var stream = File.OpenRead(fileDto.FullFileName);

        return await apiClient.UploadFileAsync(
            uploadFormData: uploadFormData,
            stream: stream,
            fileName: Path.GetFileName(fileDto.FullFileName),
            cancellationToken: cancellationToken
        );
    }
}
