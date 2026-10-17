using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.MixDrop;

public record MixDropConfig : IHosterConfig
{
    public string EmailAddress { get; init; } = null!;

    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            [nameof(EmailAddress)] = EmailAddress,
            [nameof(ApiKey)] = ApiKey,
        };
    }
}
