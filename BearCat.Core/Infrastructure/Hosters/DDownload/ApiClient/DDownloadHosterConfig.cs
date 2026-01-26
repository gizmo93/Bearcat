using BearCat.Core.Domain.Abstractions.Hoster;

namespace BearCat.Core.Infrastructure.Hosters.DDownload.ApiClient;

public record DDownloadConfig : IHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { ["ApiKey"] = ApiKey };
    }
}
