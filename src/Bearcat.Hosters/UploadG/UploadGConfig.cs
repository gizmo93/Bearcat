using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.UploadG;

public record UploadGConfig : IHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
