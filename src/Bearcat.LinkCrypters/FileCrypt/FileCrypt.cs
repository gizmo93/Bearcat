using System.Globalization;
using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.FileCrypt.Api;

namespace Bearcat.LinkCrypters.FileCrypt;

public class FileCrypt(IFileCryptApi api) : ILinkCrypterWithContainerReading
{
    private const string ContainerNotAvailableError = "this container is not available";

    private const string NoContainersMatchingError = "there are no folders matching your request";

    private const int ListContainersPageSize = 500;

    private const string MirrorKeyPrefix = "mirror_";

    public string Name => "filecrypt.cc";

    public List<string> ConfigurationKeys => [nameof(FileCryptConfig.ApiKey)];

    public bool SupportsCaptcha => true;

    public bool SupportsContainerDownload => true;

    public bool SupportsClickAndLoad => true;

    public async Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<FileCryptConfig>();

        try
        {
            var response = await api.SendAsync(
                CreateContainerRequestContent(
                    config.ApiKey,
                    containerName,
                    password,
                    enableCaptcha,
                    enableContainerDownload,
                    enableClickAndLoad,
                    linksPerMirrorNumber
                ),
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
                ErrorMessages: success ? [] : [GetErrorMessage(response)],
                StatusImageId: success ? container!.StatusImgId : null
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
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber,
        bool enableCaptcha = true,
        bool enableContainerDownload = true,
        bool enableClickAndLoad = true,
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
                    enableCaptcha: enableCaptcha,
                    enableContainerDownload: enableContainerDownload,
                    enableClickAndLoad: enableClickAndLoad,
                    linksPerMirrorNumber: linksPerMirrorNumber
                ),
                cancellationToken
            );

            if (IsSuccess(response))
            {
                return new UpdateContainerResult(
                    Outcome: UpdateContainerOutcome.Success,
                    ErrorMessage: null,
                    StatusImageId: response.Container?.FirstOrDefault()?.StatusImgId
                );
            }

            return new UpdateContainerResult(
                Outcome: IsContainerNotAvailable(response)
                    ? UpdateContainerOutcome.ContainerNotFound
                    : UpdateContainerOutcome.Failed,
                ErrorMessage: GetErrorMessage(response)
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

    public async Task<ListContainersResult> ListContainersAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = linkCrypterConfig.As<FileCryptConfig>();
        var containers = new List<ListedContainer>();

        try
        {
            var offset = 0;
            int pageCount;

            do
            {
                var response = await api.SendAsync(
                    CreateListContainersRequestContent(config.ApiKey, offset),
                    cancellationToken
                );

                if (IsNoContainersMatching(response))
                {
                    break;
                }

                if (!IsSuccess(response))
                {
                    return new ListContainersResult(
                        IsSuccess: false,
                        Containers: [],
                        ErrorMessages: [GetErrorMessage(response)]
                    );
                }

                var page = response.Container ?? [];
                containers.AddRange(page.Select(CreateListedContainer));

                pageCount = page.Count;
                offset += ListContainersPageSize;
            } while (pageCount == ListContainersPageSize);

            return new ListContainersResult(
                IsSuccess: true,
                Containers: containers,
                ErrorMessages: []
            );
        }
        catch (Exception ex)
        {
            return new ListContainersResult(
                IsSuccess: false,
                Containers: [],
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message]
            );
        }
    }

    public async Task<ReadContainerLinksResult> ReadContainerLinksAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerUrl,
        string? externalReference,
        CancellationToken cancellationToken
    )
    {
        var config = linkCrypterConfig.As<FileCryptConfig>();
        var reference = DeserializeExternalReference(externalReference, containerUrl);

        try
        {
            var response = await api.GetContainerInfoAsync(
                CreateContainerInfoRequestContent(config.ApiKey, reference.ContainerId),
                cancellationToken
            );

            if (!string.IsNullOrWhiteSpace(response.Error) || response.Container is null)
            {
                return new ReadContainerLinksResult(
                    IsSuccess: false,
                    LinksPerMirrorNumber: new Dictionary<int, IReadOnlyList<string>>(),
                    HasBackupLinks: false,
                    ErrorMessages: [response.Error ?? "FileCrypt returned no container"]
                );
            }

            return new ReadContainerLinksResult(
                IsSuccess: true,
                LinksPerMirrorNumber: response.Container.ToDictionary(
                    mirror => GetMirrorNumber(mirror.Key),
                    IReadOnlyList<string> (mirror) => mirror.Value.Links
                ),
                HasBackupLinks: response.Container.Values.Any(mirror =>
                    mirror.Backup.Any(backupLinks => backupLinks.Count > 0)
                ),
                ErrorMessages: []
            );
        }
        catch (Exception ex)
        {
            return new ReadContainerLinksResult(
                IsSuccess: false,
                LinksPerMirrorNumber: new Dictionary<int, IReadOnlyList<string>>(),
                HasBackupLinks: false,
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
        bool enableCaptcha,
        bool enableContainerDownload,
        bool enableClickAndLoad,
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber
    )
    {
        var values = CreateContainerValues(
            apiKey: apiKey,
            sub: "createV2",
            containerName: containerName,
            password: password,
            enableCaptcha: enableCaptcha,
            enableContainerDownload: enableContainerDownload,
            enableClickAndLoad: enableClickAndLoad,
            linksPerMirrorNumber: linksPerMirrorNumber
        );

        return new FormUrlEncodedContent(values);
    }

    private static FormUrlEncodedContent CreateUpdateContainerRequestContent(
        string apiKey,
        string containerId,
        string containerName,
        string? password,
        bool enableCaptcha,
        bool enableContainerDownload,
        bool enableClickAndLoad,
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber
    )
    {
        var values = CreateContainerValues(
            apiKey: apiKey,
            sub: "editV2",
            containerName: containerName,
            password: password,
            enableCaptcha: enableCaptcha,
            enableContainerDownload: enableContainerDownload,
            enableClickAndLoad: enableClickAndLoad,
            linksPerMirrorNumber: linksPerMirrorNumber
        );
        values.Add(new KeyValuePair<string, string>("container_id", containerId));

        return new FormUrlEncodedContent(values);
    }

    private static List<KeyValuePair<string, string>> CreateContainerValues(
        string apiKey,
        string sub,
        string containerName,
        string? password,
        bool enableCaptcha,
        bool enableContainerDownload,
        bool enableClickAndLoad,
        IReadOnlyDictionary<int, IReadOnlyList<string>> linksPerMirrorNumber
    )
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("api_key", apiKey),
            new("fn", "containerV2"),
            new("sub", sub),
            new("name", containerName),
            new("captcha", ToApiBool(enableCaptcha)),
            new("allow_cnl", ToApiBool(enableClickAndLoad)),
            new("allow_dlc", ToApiBool(enableContainerDownload)),
            new("allow_links", "1"),
        };

        if (!string.IsNullOrWhiteSpace(password))
        {
            values.Add(new KeyValuePair<string, string>("password", password));
        }

        values.AddRange(
            linksPerMirrorNumber
                .OrderBy(mirror => mirror.Key)
                .SelectMany(mirror =>
                    mirror.Value.Select(
                        (link, linkIndex) =>
                            new KeyValuePair<string, string>(
                                $"{MirrorKeyPrefix}{mirror.Key}[0][{linkIndex}]",
                                link
                            )
                    )
                )
        );

        return values;
    }

    private static FormUrlEncodedContent CreateListContainersRequestContent(
        string apiKey,
        int offset
    )
    {
        return new FormUrlEncodedContent([
            new KeyValuePair<string, string>("api_key", apiKey),
            new KeyValuePair<string, string>("fn", "containerV2"),
            new KeyValuePair<string, string>("sub", "listV3"),
            new KeyValuePair<string, string>(
                "offset",
                offset.ToString(CultureInfo.InvariantCulture)
            ),
            new KeyValuePair<string, string>(
                "limit",
                ListContainersPageSize.ToString(CultureInfo.InvariantCulture)
            ),
            new KeyValuePair<string, string>("sort", "created"),
            new KeyValuePair<string, string>("sort_mode", "ASC"),
        ]);
    }

    private static FormUrlEncodedContent CreateContainerInfoRequestContent(
        string apiKey,
        string containerId
    )
    {
        return new FormUrlEncodedContent([
            new KeyValuePair<string, string>("api_key", apiKey),
            new KeyValuePair<string, string>("fn", "containerV2"),
            new KeyValuePair<string, string>("sub", "info"),
            new KeyValuePair<string, string>("container_id", containerId),
        ]);
    }

    private static ListedContainer CreateListedContainer(ContainerResponse container)
    {
        var containerId = GetContainerId(container);

        return new ListedContainer(
            ContainerId: containerId,
            ContainerUrl: container.Link!,
            ContainerName: container.Name!,
            ExternalReference: SerializeExternalReference(
                new ExternalReference(ContainerId: containerId, Name: container.Name)
            )
        );
    }

    private static int GetMirrorNumber(string mirrorKey)
    {
        return int.Parse(mirrorKey[MirrorKeyPrefix.Length..], CultureInfo.InvariantCulture);
    }

    private static string ToApiBool(bool value) => value ? "1" : "0";

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

    private static bool IsNoContainersMatching(Response response) =>
        response.State == 0
        && string.Equals(response.Error, NoContainersMatchingError, StringComparison.Ordinal);

    private static bool IsContainerNotAvailable(Response response) =>
        response.State == 0
        && string.Equals(response.Error, ContainerNotAvailableError, StringComparison.Ordinal);

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
