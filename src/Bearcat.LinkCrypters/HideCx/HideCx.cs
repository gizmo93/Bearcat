using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.Extensions;
using Bearcat.LinkCrypters.HideCx.Api;
using Bearcat.LinkCrypters.HideCx.Api.CreateContainer;

namespace Bearcat.LinkCrypters.HideCx;

public class HideCx(IHideCxApi api) : ILinkCrypter
{
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
                IsSuccess: true,
                ErrorMessage: null,
                StatusImageId: externalReference
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
        return JsonSerializer.Deserialize<HideCxConfig>(serializedConfig)!;
    }

    private static string GetAuthToken(string apiKey) => $"Bearer {apiKey}";
}
