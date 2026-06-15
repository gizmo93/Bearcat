using Bearcat.Hosters.Shared.XFilesharing;

namespace Bearcat.Hosters.FileServe;

public record FileServeConfig : IXFilesharingHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
