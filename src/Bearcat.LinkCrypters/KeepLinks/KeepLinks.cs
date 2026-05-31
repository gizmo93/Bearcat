using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.KeepLinks.Api;

namespace Bearcat.LinkCrypters.KeepLinks;

public class KeepLinks(IKeepLinksApi api) : ILinkCrypter
{
    public string Name => "keeplinks.org";
    public List<string> ConfigurationKeys => [nameof(KeepLinksConfig.ApiKey)];

    public async Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyList<string> links,
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
                    title: containerName
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
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<KeepLinksConfig>();

        try
        {
            var response = await api.UpdateContainerAsync(
                request: CreateRequestContent(
                    apiKey: config.ApiKey,
                    links: links,
                    password: password,
                    urlId: containerLink.Split('/').Last()
                ),
                cancellationToken: cancellationToken
            );

            var success = response.ApiError is null;

            return new UpdateContainerResult(IsSuccess: success, ErrorMessage: response.ApiError);
        }
        catch (Exception ex)
        {
            return new UpdateContainerResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    private static MultipartFormDataContent CreateRequestContent(
        string apiKey,
        IReadOnlyList<string> links,
        string? password = null,
        string? title = null,
        string? urlId = null
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

        return content;
    }

    private static void AddFormField(MultipartFormDataContent content, string name, string value)
    {
        var field = new StringContent(value);
        field.Headers.ContentType = null;

        content.Add(field, name);
    }
}
