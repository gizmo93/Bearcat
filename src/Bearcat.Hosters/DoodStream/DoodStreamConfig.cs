using Bearcat.Hosters.Shared.XVideoSharing;

namespace Bearcat.Hosters.DoodStream;

public record DoodStreamConfig : IXVideoSharingHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
