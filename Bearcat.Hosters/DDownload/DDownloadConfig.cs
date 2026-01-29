using Bearcat.Domain.Abstractions.Hoster;

namespace Bearcat.Hosters.DDownload;

public record DDownloadConfig : IHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
