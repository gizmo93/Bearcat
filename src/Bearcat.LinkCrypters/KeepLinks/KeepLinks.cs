using System.Text.Json;
using System.Xml.Linq;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.KeepLinks.Api;
using Bearcat.LinkCrypters.KeepLinks.Api.ProtectLinks;

namespace Bearcat.LinkCrypters.KeepLinks;

public class KeepLinks(IKeepLinksApi api) : ILinkCrypter
{
    private const string ApiHashOrUrlIdNotValidError = "API hash or URL ID is not valid";

    public string Name => "keeplinks.org";
    public List<string> ConfigurationKeys => [nameof(KeepLinksConfig.ApiKey)];

    public bool SupportsCaptcha => true;

    public bool SupportsContainerDownload => true;

    public bool SupportsClickAndLoad => false;

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
        var config = linkCrypterConfig.As<KeepLinksConfig>();

        try
        {
            var response = await api.ProtectLinkAsync(
                request: CreateRequestContent(
                    apiKey: config.ApiKey,
                    links: links,
                    password: password,
                    title: containerName,
                    enableCaptcha: enableCaptcha,
                    enableContainerDownload: enableContainerDownload
                ),
                cancellationToken: cancellationToken
            );

            var success = response.ApiError is null;

            return new CreateContainerResult(
                IsSuccess: success,
                ContainerLink: response.ContainerLink,
                ExternalReference: null,
                ErrorMessages: !success ? [response.ApiError ?? "Unknown error"] : []
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

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(config);
    }

    public ILinkCrypterConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<KeepLinksConfig>(serializedConfig)!;
    }

    public async Task<TryLoginResult> TryLoginAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<KeepLinksConfig>();

        const string loginErrorMessage = "API hash is not valid";

        try
        {
            var response = await api.GetLinksAsync(
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken
            );

            var success = !response.Contains(loginErrorMessage);

            return new TryLoginResult(
                IsSuccess: success,
                ErrorMessage: success ? null : loginErrorMessage
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
        var config = linkCrypterConfig.As<KeepLinksConfig>();

        var urlId = containerLink.Split('/').Last();

        try
        {
            var responseBody = await api.UpdateContainerAsync(
                request: CreateRequestContent(
                    apiKey: config.ApiKey,
                    links: links,
                    password: password,
                    urlId: urlId,
                    enableCaptcha: enableCaptcha,
                    enableContainerDownload: enableContainerDownload
                ),
                cancellationToken: cancellationToken
            );

            var apiError = ReadUpdateApiError(responseBody);

            if (apiError is null)
            {
                return new UpdateContainerResult(
                    Outcome: UpdateContainerOutcome.Success,
                    ErrorMessage: null
                );
            }

            var containerIsMissing =
                apiError == ApiHashOrUrlIdNotValidError
                && await IsContainerMissingAsync(
                    apiKey: config.ApiKey,
                    urlId: urlId,
                    cancellationToken: cancellationToken
                );

            return new UpdateContainerResult(
                Outcome: containerIsMissing
                    ? UpdateContainerOutcome.ContainerNotFound
                    : UpdateContainerOutcome.Failed,
                ErrorMessage: apiError
            );
        }
        catch (Exception ex)
        {
            return new UpdateContainerResult(
                Outcome: UpdateContainerOutcome.Failed,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    private static string? ReadUpdateApiError(string responseBody)
    {
        if (!responseBody.TrimStart().StartsWith('<'))
        {
            return JsonSerializer.Deserialize<Response>(responseBody)!.ApiError;
        }

        var apiError = XDocument.Parse(responseBody).Descendants("api_error").FirstOrDefault();

        return apiError?.Value ?? $"KeepLinks returned an unexpected XML response: {responseBody}";
    }

    private async Task<bool> IsContainerMissingAsync(
        string apiKey,
        string urlId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var responseBody = await api.GetContainerAsync(
                apiKey: apiKey,
                urlId: urlId,
                cancellationToken: cancellationToken
            );

            using var document = JsonDocument.Parse(responseBody);

            return document.RootElement.ValueKind == JsonValueKind.Array
                && document.RootElement.GetArrayLength() == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static MultipartFormDataContent CreateRequestContent(
        string apiKey,
        IReadOnlyList<string> links,
        string? password = null,
        string? title = null,
        string? urlId = null,
        bool enableCaptcha = true,
        bool enableContainerDownload = true
    )
    {
        var content = new MultipartFormDataContent();

        AddFormField(content, "apihash", apiKey);
        AddFormField(content, "output", "json");

        AddFormField(content, "link-to-protect", string.Join(',', links));

        if (!string.IsNullOrWhiteSpace(password))
        {
            AddFormField(content, "password", password);
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            AddFormField(content, "title", title);
        }

        if (!string.IsNullOrWhiteSpace(urlId))
        {
            AddFormField(content, "url-id", urlId);
        }

        if (enableCaptcha)
        {
            AddFormField(content, "captcha", "on");
            AddFormField(content, "captchatype", "Re");
        }

        if (enableContainerDownload)
        {
            AddFormField(content, "dlc", "on");
        }

        return content;
    }

    private static void AddFormField(MultipartFormDataContent content, string name, string value)
    {
        var field = new StringContent(value);
        field.Headers.ContentType = null;

        content.Add(field, name);
    }
}
