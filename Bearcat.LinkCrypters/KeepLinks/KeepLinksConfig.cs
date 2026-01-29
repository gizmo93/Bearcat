using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter;

namespace Bearcat.LinkCrypters.KeepLinks;

public record KeepLinksConfig : ILinkCrypterConfig
{
    public string ApiKey { get; init; } = null!;
    
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            [nameof(ApiKey)] = ApiKey,
        };
    }
}
