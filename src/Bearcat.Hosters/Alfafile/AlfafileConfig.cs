using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Alfafile;

public record AlfafileConfig : IHosterConfig
{
    public string Username { get; init; } = null!;

    public string Password { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            [nameof(Username)] = Username,
            [nameof(Password)] = Password,
        };
    }
}
