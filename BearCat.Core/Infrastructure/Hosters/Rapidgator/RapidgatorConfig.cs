using BearCat.Core.Domain.Abstractions;
using BearCat.Core.Domain.Abstractions.Hoster;

namespace BearCat.Core.Infrastructure.Hosters.Rapidgator;

public record RapidgatorConfig : IHosterConfig
{
    public string Username { get; init; } = null!;

    public string Password { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { { "Username", Username }, { "Password", Password } };
    }
}
