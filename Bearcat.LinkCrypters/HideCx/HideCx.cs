using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.HideCx.ApiClient;

namespace Bearcat.LinkCrypters.HideCx;

public class HideCx(IHideCxApi api)
    : ILinkCrypter
{
    public string Name => "Hide.cx";

    public List<string> ConfigurationKeys => ["ApiKey"];

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
                Mirrors = links
            };

            var result = await api.CreateContainerAsync(
                request: request,
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken);

            return new CreateContainerResult(
                IsSuccess: true,
                ContainerLink: result.CanonicalUrl,
                ErrorMessages: []);
        }
        catch (Exception ex)
        {
            return new CreateContainerResult(
                IsSuccess: false,
                ContainerLink: null,
                ErrorMessages: [ex.Message]);
        }
    }

    public string SerializeConfig(IReadOnlyDictionary<string, object> config)
    {
        return JsonSerializer.Serialize(config);
    }

    public ILinkCrypterConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<HideCxConfig>(serializedConfig)!;
    }
}
