using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Keep2Share;

public record Keep2ShareConfig : IHosterConfig
{
    public string EmailAddress { get; init; } = null!;

    public string Password { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            [nameof(EmailAddress)] = EmailAddress,
            [nameof(Password)] = Password,
        };
    }
}
