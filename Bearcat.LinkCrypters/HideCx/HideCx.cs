using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.HideCx.ApiClient;

namespace Bearcat.LinkCrypters.HideCx;

public class HideCx(IHideCxApi api)
    : ILinkCrypter
{
    public string Name => "Hide.cx";

    public List<string> ConfigurationKeys => [nameof(HideCxConfig.ApiKey)];

    public async Task<CreateContainerResult> CreateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerName,
        string? password,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken = default)
    {
        if (linkCrypterConfig is not HideCxConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(linkCrypterConfig)} to be of type {nameof(HideCxConfig)}",
                nameof(linkCrypterConfig));
        }

        try
        {
            var request = new ApiClient.CreateContainer.Request
            {
                Name = containerName,
                Password = password,
                Mirrors = [links.ToArray()]
            };

            var result = await api.CreateContainerAsync(
                request: request,
                apiToken: GetAuthToken(config.ApiKey),
                cancellationToken: cancellationToken);

            return new CreateContainerResult(
                IsSuccess: true,
                ContainerLink: result.CanonicalUrl,
                ExternalReference: result.Id,
                ErrorMessages: []);
        }
        catch (Exception ex)
        {
            return new CreateContainerResult(
                IsSuccess: false,
                ContainerLink: null,
                ExternalReference: null,
                ErrorMessages: [ex.Message]);
        }
    }

    public async Task<TryLoginResult> TryLoginAsync(
        ILinkCrypterConfig linkCrypterConfig,
        CancellationToken cancellationToken = default)
    {
        if (linkCrypterConfig is not HideCxConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(linkCrypterConfig)} to be of type {nameof(HideCxConfig)}",
                nameof(linkCrypterConfig));
        }

        // As hide.cx does not provide a dedicated login endpoint, we abuse the container search
        // to do a login check
        try
        {
            var request = new ApiClient.SearchContainers.Request
            {
                Limit = 1,
                Offset = 0,
                Search = string.Empty,
                PrimaryType = null,
                AccessStatus = "unknown",
                OrderBy = "created_at",
                OrderType = "desc"
            };

            await api.SearchContainersAsync(
                request: request,
                apiToken: GetAuthToken(config.ApiKey),
                cancellationToken: cancellationToken);

            return new TryLoginResult(
                IsSuccess: true,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.Message);
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
