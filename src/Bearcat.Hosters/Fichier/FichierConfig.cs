using Bearcat.Abstractions.Hoster;

namespace Bearcat.Hosters.Fichier;

public record FichierConfig : IHosterConfig
{
    public string ApiKey { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { { nameof(ApiKey), ApiKey } };
    }
}
