using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.DistributionSites.Extensions;
using Bearcat.DistributionSites.Shared.XenForo;

namespace Bearcat.DistributionSites.GenericXenForo;

public sealed class GenericXenForo(IHttpClientFactory httpClientFactory)
    : XenForoDistributionSiteBase<GenericXenForoConfig>(httpClientFactory)
{
    public override string Name => "XenForo";

    public override IReadOnlyList<ConfigurationField> ConfigurationFields =>
        new List<ConfigurationField>
        {
            new(nameof(GenericXenForoConfig.BaseUrl), ConfigurationFieldType.Url, IsRequired: true),
        }
            .Concat(base.ConfigurationFields)
            .Append(
                new ConfigurationField(
                    nameof(GenericXenForoConfig.RemoveAttachmentsOnPostUpdate),
                    ConfigurationFieldType.Boolean,
                    IsRequired: false,
                    DefaultValue: false
                )
            )
            .ToList();

    protected override bool ShouldRemoveAttachmentsOnPostUpdate(GenericXenForoConfig config)
    {
        return config.RemoveAttachmentsOnPostUpdate;
    }

    public override string GetBaseUrl(IDistributionSiteConfig config) =>
        config.As<GenericXenForoConfig>().BaseUrl;
}
