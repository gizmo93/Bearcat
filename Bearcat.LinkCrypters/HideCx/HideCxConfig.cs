using Bearcat.Abstractions.LinkCrypter;

namespace Bearcat.LinkCrypters.HideCx;

public record HideCxConfig : ILinkCrypterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            ["ApiKey"] = ApiKey
        };
    }
}
