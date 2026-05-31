using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.FileCrypt.Api;

namespace Bearcat.LinkCrypters.FileCrypt;

public class FileCrypt(IFileCryptApi api) : ILinkCrypter
{
    public string Name => "filecrypt.cc";

    public List<string> ConfigurationKeys => [nameof(FileCryptConfig.ApiKey)];

    public async Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<FileCryptConfig>();

        try
        {
            var response = await api.SendAsync(
                CreateContainerRequestContent(config.ApiKey, containerName, password, links),
                cancellationToken
            );

            var container = response.Container?.FirstOrDefault();

            var success = IsSuccess(response) && !string.IsNullOrWhiteSpace(container?.Link);

            var externalReference =
                success && container is not null
                    ? SerializeExternalReference(
                        new ExternalReference(
                            ContainerId: GetContainerId(container),
                            Name: container.Name ?? containerName
                        )
                    )
                    : null;

            return new CreateContainerResult(
                IsSuccess: success,
                ContainerLink: success ? container!.Link : null,
                ExternalReference: externalReference,
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
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<FileCryptConfig>();
        var reference = DeserializeExternalReference(externalReference, containerLink);

        try
        {
            var response = await api.SendAsync(
                CreateUpdateContainerRequestContent(
                    apiKey: config.ApiKey,
                    containerId: reference.ContainerId,
                    containerName: reference.Name ?? reference.ContainerId,
                    password: password,
                    links: links
                ),
                cancellationToken
            );

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
        return JsonSerializer.Deserialize<FileCryptConfig>(serializedConfig)!;
    }

    public async Task<TryLoginResult> TryLoginAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<FileCryptConfig>();

        try
        {
            var response = await api.SendAsync(
                CreateApiKeyRequestContent(config.ApiKey),
                cancellationToken
            );

            var success = IsSuccess(response) && !string.IsNullOrWhiteSpace(response.Key);

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

    private static FormUrlEncodedContent CreateContainerRequestContent(
        string apiKey,
        string containerName,
        string? password,
        IReadOnlyList<string> links
    )
    {
        var values = CreateContainerValues(
            apiKey: apiKey,
            sub: "createV2",
            containerName: containerName,
            password: password,
            links: links
        );

        return new FormUrlEncodedContent(values);
    }

    private static FormUrlEncodedContent CreateUpdateContainerRequestContent(
        string apiKey,
        string containerId,
        string containerName,
        string? password,
        IReadOnlyList<string> links
    )
    {
        var values = CreateContainerValues(
            apiKey: apiKey,
            sub: "editV2",
            containerName: containerName,
            password: password,
            links: links
        );
        values.Add(new KeyValuePair<string, string>("container_id", containerId));

        return new FormUrlEncodedContent(values);
    }

    private static List<KeyValuePair<string, string>> CreateContainerValues(
        string apiKey,
        string sub,
        string containerName,
        string? password,
        IReadOnlyList<string> links
    )
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("api_key", apiKey),
            new("fn", "containerV2"),
            new("sub", sub),
            new("name", containerName),
            new("captcha", "0"),
            new("allow_cnl", "1"),
            new("allow_dlc", "1"),
            new("allow_links", "1"),
        };

        if (!string.IsNullOrWhiteSpace(password))
        {
            values.Add(new KeyValuePair<string, string>("password", password));
        }

        values.AddRange(
            links.Select((t, i) => new KeyValuePair<string, string>($"mirror_1[0][{i}]", t))
        );

        return values;
    }

    private static FormUrlEncodedContent CreateApiKeyRequestContent(string apiKey)
    {
        return new FormUrlEncodedContent([
            new KeyValuePair<string, string>("api_key", apiKey),
            new KeyValuePair<string, string>("fn", "user"),
            new KeyValuePair<string, string>("sub", "apikey"),
        ]);
    }

    private static bool IsSuccess(Response response) =>
        response.State == 1 && string.IsNullOrWhiteSpace(response.Error);

    private static string GetErrorMessage(Response response)
    {
        if (!string.IsNullOrWhiteSpace(response.Error))
        {
            return response.Error;
        }

        return response.State != 1 ? $"FileCrypt returned state {response.State}" : "Unknown error";
    }

    private static string GetContainerId(ContainerResponse container)
    {
        return !string.IsNullOrWhiteSpace(container.Id)
            ? container.Id
            : GetContainerIdFromLink(container.Link!);
    }

    private static string GetContainerIdFromLink(string containerLink)
    {
        var lastSegment = Uri.TryCreate(containerLink, UriKind.Absolute, out var uri)
            ? uri.Segments.Last()
            : containerLink.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

        return lastSegment.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? lastSegment[..^".html".Length]
            : lastSegment.Trim('/');
    }

    private static string SerializeExternalReference(ExternalReference reference)
    {
        return JsonSerializer.Serialize(reference);
    }

    private static ExternalReference DeserializeExternalReference(
        string? externalReference,
        string containerLink
    )
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return new ExternalReference(GetContainerIdFromLink(containerLink), Name: null);
        }

        try
        {
            var reference = JsonSerializer.Deserialize<ExternalReference>(externalReference);
            if (!string.IsNullOrWhiteSpace(reference?.ContainerId))
            {
                return reference;
            }
        }
        catch (JsonException)
        {
            return new ExternalReference(externalReference, Name: null);
        }

        return new ExternalReference(GetContainerIdFromLink(containerLink), Name: null);
    }

    private sealed record ExternalReference(string ContainerId, string? Name);
}
