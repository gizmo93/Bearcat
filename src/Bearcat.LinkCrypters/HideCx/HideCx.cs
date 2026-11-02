using System.Globalization;
using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.HideCx.Api;
using Bearcat.LinkCrypters.HideCx.Api.CreateContainer;
using Refit;

namespace Bearcat.LinkCrypters.HideCx;

public class HideCx(IHideCxApi api) : ILinkCrypterWithContainerReading
{
    private const int ListContainersPageSize = 500;

    private const string DeletedAccessStatus = "deleted";

    private const string MirrorLinkTypePrefix = "mirror_";

    public string Name => "Hide.cx";

    public List<string> ConfigurationKeys => [nameof(HideCxConfig.ApiKey)];

    public bool SupportsCaptcha => false;

    public bool SupportsContainerDownload => false;

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
        var config = linkCrypterConfig.As<HideCxConfig>();

        try
        {
            var request = new Request
            {
                Name = containerName,
                Password = password,
                Mirrors = [links.ToArray()],
            };

            var result = await api.CreateContainerAsync(
                request: request,
                apiToken: GetAuthToken(config.ApiKey),
                cancellationToken: cancellationToken
            );

            return new CreateContainerResult(
                IsSuccess: true,
                ContainerLink: result.CanonicalUrl,
                ExternalReference: result.Id,
                ErrorMessages: [],
                StatusImageId: result.Id
            );
        }
        catch (Exception ex)
        {
            return new CreateContainerResult(
                IsSuccess: false,
                ContainerLink: null,
                ExternalReference: null,
                ErrorMessages: [ex.Message]
            );
        }
    }

    public async Task<TryLoginResult> TryLoginAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = linkCrypterConfig.As<HideCxConfig>();

        // As hide.cx does not provide a dedicated login endpoint, we abuse the container search
        // to do a login check
        try
        {
            var request = new Api.SearchContainers.Request
            {
                Limit = 1,
                Offset = 0,
                Search = string.Empty,
                PrimaryType = null,
                AccessStatus = "unknown",
                OrderBy = "created_at",
                OrderType = "desc",
            };

            await api.SearchContainersAsync(
                request: request,
                apiToken: GetAuthToken(config.ApiKey),
                cancellationToken: cancellationToken
            );

            return new TryLoginResult(IsSuccess: true, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new TryLoginResult(IsSuccess: false, ErrorMessage: ex.Message);
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
        var config = linkCrypterConfig.As<HideCxConfig>();

        try
        {
            await api.UpdateContainerAsync(
                containerId: externalReference!,
                request: new Api.UpdateContainer.Request { Mirrors = [links.ToArray()] },
                apiToken: GetAuthToken(config.ApiKey),
                cancellationToken: cancellationToken
            );

            return new UpdateContainerResult(
                Outcome: UpdateContainerOutcome.Success,
                ErrorMessage: null,
                StatusImageId: externalReference
            );
        }
        catch (Exception ex)
        {
            var containerIsMissing = await IsContainerMissingAsync(
                containerId: externalReference!,
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken
            );

            return new UpdateContainerResult(
                Outcome: containerIsMissing
                    ? UpdateContainerOutcome.ContainerNotFound
                    : UpdateContainerOutcome.Failed,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    public async Task<ListContainersResult> ListContainersAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken
    )
    {
        var config = linkCrypterConfig.As<HideCxConfig>();
        var containers = new List<ListedContainer>();

        try
        {
            var offset = 0;
            int pageCount;

            do
            {
                var response = await api.SearchContainersAsync(
                    request: new Api.SearchContainers.Request
                    {
                        Search = string.Empty,
                        PrimaryType = null,
                        AccessStatus = null,
                        Offset = offset,
                        Limit = ListContainersPageSize,
                        OrderBy = "created_at",
                        OrderType = "asc",
                    },
                    apiToken: GetAuthToken(config.ApiKey),
                    cancellationToken: cancellationToken
                );

                containers.AddRange(
                    response
                        .Containers.Where(container =>
                            container.AccessStatus != DeletedAccessStatus
                        )
                        .Select(container => new ListedContainer(
                            ContainerId: container.Id,
                            ContainerUrl: container.CanonicalUrl,
                            ContainerName: container.Name,
                            ExternalReference: container.Id
                        ))
                );

                pageCount = response.Containers.Count;
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
        var config = linkCrypterConfig.As<HideCxConfig>();
        var containerId =
            externalReference
            ?? containerUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

        try
        {
            var content = await api.GetContainerAsync(
                containerId: containerId,
                apiToken: GetAuthToken(config.ApiKey),
                cancellationToken: cancellationToken
            );

            var container = JsonSerializer.Deserialize<Api.GetContainer.Response>(content)!;

            if (container.Links.Any(link => link.HosterUrl is null))
            {
                return new ReadContainerLinksResult(
                    IsSuccess: false,
                    LinksPerMirror: [],
                    HasBackupLinks: false,
                    ErrorMessages:
                    [
                        "Hide.cx returned links without hoster_url, which happens when the API key does not belong to the container owner",
                    ]
                );
            }

            var linksPerMirror = container
                .Links.GroupBy(link => link.LinkType)
                .OrderBy(mirror => GetMirrorNumber(mirror.Key))
                .Select(mirror => mirror.Select(link => link.HosterUrl!).ToList())
                .ToList();

            return new ReadContainerLinksResult(
                IsSuccess: true,
                LinksPerMirror: linksPerMirror,
                HasBackupLinks: false,
                ErrorMessages: []
            );
        }
        catch (Exception ex)
        {
            return new ReadContainerLinksResult(
                IsSuccess: false,
                LinksPerMirror: [],
                HasBackupLinks: false,
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message]
            );
        }
    }

    private async Task<bool> IsContainerMissingAsync(
        string containerId,
        string apiKey,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await api.GetContainerAsync(
                containerId: containerId,
                apiToken: GetAuthToken(apiKey),
                cancellationToken: cancellationToken
            );

            return false;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(config);
    }

    public ILinkCrypterConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<HideCxConfig>(serializedConfig)!;
    }

    private static int GetMirrorNumber(string linkType)
    {
        return int.Parse(linkType[MirrorLinkTypePrefix.Length..], CultureInfo.InvariantCulture);
    }

    private static string GetAuthToken(string apiKey) => $"Bearer {apiKey}";
}
