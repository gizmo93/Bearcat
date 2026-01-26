using Bearcat.Domain.Abstractions.Hoster;

namespace Bearcat.Hosters.Rapidgator;

public record RapidgatorConfig : IHosterConfig
{
    public string Username { get; init; } = null!;

    public string Password { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { { "Username", Username }, { "Password", Password } };
    }
}
