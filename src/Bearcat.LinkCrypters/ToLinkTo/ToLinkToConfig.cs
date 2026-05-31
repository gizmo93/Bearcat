using Bearcat.Abstractions.LinkCrypter;

namespace Bearcat.LinkCrypters.ToLinkTo;

public record ToLinkToConfig : ILinkCrypterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
