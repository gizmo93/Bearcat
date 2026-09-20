using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.DistributionSites.Extensions;
using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.XenForo;

public sealed class XenForo(IHttpClientFactory httpClientFactory)
    : XenForoDistributionSiteBase<XenForoConfig>(httpClientFactory)
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;

    private XenForo(IHttpClientFactory httpClientFactory, string baseUrl)
        : this(httpClientFactory)
    {
        this.BaseUrl = baseUrl;
    }

    public override string Name => "XenForo (generic)";

    public override string BaseUrl =>
        field ?? throw new InvalidOperationException("The XenForo site has not been configured.");

    public override IReadOnlyList<DistributionSiteConfigurationField> ConfigurationFields =>
        [
            new(
                Key: nameof(XenForoConfig.BaseUrl),
                LabelResourceKey: "ForumBaseUrl",
                Placeholder: "https://example.org/community/",
                PrefillOnEdit: true
            ),
            .. base.ConfigurationFields,
        ];

    public override string ConfigurationHelpResourceKey => "GenericXenForoHelp";

    public override IDistributionSiteConfig DeserializeConfig(string serializedConfig)
    {
        var config = base.DeserializeConfig(serializedConfig).As<XenForoConfig>();

        if (
            !Uri.TryCreate(config.BaseUrl?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.AbsolutePath.EndsWith(".php", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ValidationException(
                "Enter the forum's HTTP or HTTPS base URL, including its installation directory, without a query, fragment or index.php."
            );
        }

        return config with
        {
            BaseUrl = uri.AbsoluteUri.TrimEnd('/') + "/",
        };
    }

    public override string SerializeConfig(Dictionary<string, string> config)
    {
        var validatedConfig = DeserializeConfig(JsonSerializer.Serialize(config))
            .As<XenForoConfig>();

        return JsonSerializer.Serialize(validatedConfig);
    }

    public override IDistributionSite WithConfiguration(IDistributionSiteConfig config)
    {
        return new XenForo(httpClientFactory, config.As<XenForoConfig>().BaseUrl);
    }
}
