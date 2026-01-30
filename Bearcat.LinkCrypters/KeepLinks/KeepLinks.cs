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
        IReadOnlyList<string> links, CancellationToken cancellationToken = default)
    {
        var config = linkCrypterConfig.As<KeepLinksConfig>();

        try
        {
            var linksString = string.Join(',', links);
            
            var response = await api.ProtectLinkAsync(
                apiKey: config.ApiKey,
                linksToProtect: linksString,
                password: password,
                title: containerName,
                cancellationToken: cancellationToken);

            var success = response.ApiError is null;

            return new CreateContainerResult(
                IsSuccess: success,
                ContainerLink: response.ContainerLink,
                ExternalReference: null,
                ErrorMessages: !success
                    ? [response.ApiError ?? "Unknown error"]
                    : []);
        }
        catch (Exception ex)
        {
            return new CreateContainerResult(
                IsSuccess: false,
                ContainerLink: null,
                ExternalReference: null,
                ErrorMessages: [ex.InnerException?.Message ?? ex.Message]);
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
        CancellationToken cancellationToken = default)
    {
        var config = linkCrypterConfig.As<KeepLinksConfig>();
        
        const string loginErrorMessage = "API hash is not valid";
        
        try
        {
            var response = await api.GetLinksAsync(
                apiKey: config.ApiKey,
                cancellationToken: cancellationToken);
            
            var success = !response.Contains(loginErrorMessage);
            
            return new TryLoginResult(
                IsSuccess: success,
                ErrorMessage: loginErrorMessage);
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message);
        }
    }

    public async Task<UpdateContainerResult> UpdateContainerAsync(
        ILinkCrypterConfig linkCrypterConfig,
        string containerLink,
        string? externalReference,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken = default)
    {
        var config = linkCrypterConfig.As<KeepLinksConfig>();

        try
        {
            var linksString = string.Join(',', links);
            
            var response = await api.UpdateContainerAsync(
                apiKey: config.ApiKey,
                linksToProtect: linksString,
                urlId: containerLink.Split('/').Last(),
                cancellationToken: cancellationToken);

            var success = response.ApiError is null;

            return new UpdateContainerResult(
                IsSuccess: success,
                ErrorMessage: response.ApiError);
        }
        catch (Exception ex)
        {
            return new UpdateContainerResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message);
        }
    }
}
