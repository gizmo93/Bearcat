using Bearcat.Hosters.Shared.CostAction;

namespace Bearcat.Hosters.Hitfile;

public record HitfileConfig : ICostActionHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
