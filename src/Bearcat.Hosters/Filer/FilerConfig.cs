using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Filer;

public record FilerConfig : IHosterConfig
{
    public string Email { get; init; } = null!;

    public string Password { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            [nameof(Email)] = Email,
            [nameof(Password)] = Password,
        };
    }
}
