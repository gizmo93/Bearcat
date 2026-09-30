using System.Text.Json;

namespace Bearcat.DistributionSites.Shared.XenForo;

internal static class XenForoConfigSerializerOptions
{
    public static readonly JsonSerializerOptions CaseInsensitivePropertyNames = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
