using Bearcat.Abstractions.LinkCrypter;

namespace Bearcat.LinkCrypters.FileCrypt;

public record FileCryptConfig : ILinkCrypterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
