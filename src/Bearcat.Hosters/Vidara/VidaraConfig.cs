using Bearcat.Hosters.Shared.XVideoSharing;

namespace Bearcat.Hosters.Vidara;

public record VidaraConfig : IXVideoSharingHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
