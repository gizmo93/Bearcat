using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.ToLinkTo.Api;
using Bearcat.LinkCrypters.ToLinkTo.Api.CreateFolder;

namespace Bearcat.LinkCrypters.ToLinkTo;

public class ToLinkTo(IToLinkToApi api) : ILinkCrypter
{
    public string Name => "tolink.to";

    public List<string> ConfigurationKeys => [nameof(ToLinkToConfig.ApiKey)];

    public bool SupportsCaptcha => true;

    public bool SupportsContainerDownload => true;

    public bool SupportsClickAndLoad => true;

    public async Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyList<string> links,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<ToLinkToConfig>();

        try
        {
            var result = await api.CreateFolderAsync(
                request: new ApiRequest<RequestBody>
                {
                    ApiKey = config.ApiKey,
                    Body = new RequestBody
                    {
                        Title = containerName,
                        Links = CreateLinksValue(links),
                        Options = CreateFolderOptions(
                            password,
                            enableCaptcha,
                            enableContainerDownload,
                            enableClickAndLoad
                        ),
                    },
                },
                cancellationToken: cancellationToken
            );

            var response = result.Response;
            var success = IsSuccess(response) && !string.IsNullOrWhiteSpace(response.Body);

            return new CreateContainerResult(
                IsSuccess: success,
                ContainerLink: success ? response.Body : null,
                ExternalReference: success ? GetFolderAlias(response.Body!) : null,
                ErrorMessages: success ? [] : [GetErrorMessage(response)]
            );
        }
        catch (Exception ex)
        {
            return new CreateContainerResult(
                IsSuccess: false,
                ContainerLink: null,
                ExternalReference: null,
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message]
            );
        }
    }

    public async Task<UpdateContainerResult> UpdateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerLink,
        string? externalReference,
        string? password,
        IReadOnlyList<string> links,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<ToLinkToConfig>();

        try
        {
            var folder = !string.IsNullOrWhiteSpace(externalReference)
                ? externalReference
                : GetFolderAlias(containerLink);

            var result = await api.EditFolderAsync(
                request: new ApiRequest<Api.EditFolder.RequestBody>
                {
                    ApiKey = config.ApiKey,
                    Body = new Api.EditFolder.RequestBody
                    {
                        Folder = folder,
                        Title = folder,
                        Links = CreateLinksValue(links),
                        Options = CreateFolderOptions(
                            password: password,
                            enableCaptcha: enableCaptcha,
                            enableContainerDownload: enableContainerDownload,
                            enableClickAndLoad: enableClickAndLoad
                        ),
                    },
                },
                cancellationToken: cancellationToken
            );

            var response = result.Response;
            var success = IsSuccess(response);

            return new UpdateContainerResult(
                IsSuccess: success,
                ErrorMessage: success ? null : GetErrorMessage(response)
            );
        }
        catch (Exception ex)
        {
            return new UpdateContainerResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(config);
    }

    public ILinkCrypterConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<ToLinkToConfig>(serializedConfig)!;
    }

    public async Task<TryLoginResult> TryLoginAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<ToLinkToConfig>();

        try
        {
            var result = await api.PingAsync(
                request: new ApiRequest<Api.Ping.RequestBody>
                {
                    ApiKey = config.ApiKey,
                    Body = new Api.Ping.RequestBody { Message = "Ping" },
                },
                cancellationToken: cancellationToken
            );

            var response = result.Response;
            var success = IsSuccess(response) && response.Body == "Pong";

            return new TryLoginResult(
                IsSuccess: success,
                ErrorMessage: success ? null : GetErrorMessage(response)
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

    private static FolderOptions CreateFolderOptions(
        string? password,
        bool enableCaptcha,
        bool enableContainerDownload,
        bool enableClickAndLoad
    )
    {
        return new FolderOptions
        {
            Web = true,
            Container = enableContainerDownload,
            ClickAndLoad = enableClickAndLoad,
            Captcha = enableCaptcha,
            CaptchaText = false,
            Password = password ?? string.Empty,
        };
    }

    private static string CreateLinksValue(IReadOnlyList<string> links) => string.Join(';', links);

    private static bool IsSuccess<TBody>(ApiResponseContent<TBody> response)
    {
        return response is { Status: "OK", ErrorCode: 0 };
    }

    private static string GetErrorMessage<TBody>(ApiResponseContent<TBody> response)
    {
        if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
        {
            return response.ErrorMessage;
        }

        return !string.IsNullOrWhiteSpace(response.Status)
            ? $"ToLink.to returned status {response.Status} with error code {response.ErrorCode}"
            : "Unknown error";
    }

    private static string GetFolderAlias(string folderLinkOrAlias)
    {
        return Uri.TryCreate(folderLinkOrAlias, UriKind.Absolute, out var uri)
            ? uri.Segments.Last().Trim('/')
            : folderLinkOrAlias.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
    }
}
