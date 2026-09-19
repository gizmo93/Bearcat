using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Steam.Api;
using Refit;

namespace Bearcat.MediaDatabases.Steam;

public class SteamMetadataDatabase(ISteamApi api) : IMediaMetadataDatabase
{
    private const string DefaultLanguage = "english";
    private const string GermanLanguage = "german";
    private const string ReachabilityCheckAppId = "570";

    private static readonly Dictionary<string, string> Languages = new()
    {
        ["de"] = GermanLanguage,
        ["en"] = DefaultLanguage,
        ["fr"] = "french",
        ["es"] = "spanish",
        ["it"] = "italian",
        ["ja"] = "japanese",
        ["ko"] = "korean",
        ["ru"] = "russian",
        ["nl"] = "dutch",
    };

    public string Name => "Steam";

    public int ResolutionPriority => 100;

    public IReadOnlyList<MediaKind> SupportedMediaKinds => [MediaKind.Game];

    public IReadOnlyList<string> ConfigurationKeys => [];

    public async Task<MediaMetadata?> GetByExternalIdAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(lookup.SteamAppId))
        {
            return null;
        }

        return await GetAppMetadataAsync(
            appId: lookup.SteamAppId,
            language: ResolveLanguage(lookup.LanguageCode),
            cancellationToken: cancellationToken
        );
    }

    public async Task<MediaMetadata?> GetByTitleAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(lookup.Title))
        {
            return null;
        }

        var language = ResolveLanguage(lookup.LanguageCode);
        var search = await SendAsync(
            api.SearchStoreAsync(
                term: lookup.Title,
                language: language,
                countryCode: language == GermanLanguage ? "DE" : "US",
                cancellationToken: cancellationToken
            )
        );
        var item = search?.Items?.FirstOrDefault();

        if (item is null)
        {
            return null;
        }

        return await GetAppMetadataAsync(
            appId: item.Id.ToString(),
            language: language,
            cancellationToken: cancellationToken
        );
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IMediaMetadataDatabaseConfig config,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await SendAsync(
                api.GetAppDetailsAsync(
                    appIds: ReachabilityCheckAppId,
                    language: DefaultLanguage,
                    cancellationToken: cancellationToken
                )
            );

            return response?.GetValueOrDefault(ReachabilityCheckAppId)?.Success == true
                ? new TryLoginResult(true, null)
                : new TryLoginResult(false, "Steam store is not reachable.");
        }
        catch (Exception exception)
        {
            return new TryLoginResult(false, exception.Message);
        }
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>());
    }

    public IMediaMetadataDatabaseConfig DeserializeConfig(string serializedConfig)
    {
        return new SteamConfig();
    }

    private async Task<MediaMetadata?> GetAppMetadataAsync(
        string appId,
        string language,
        CancellationToken cancellationToken
    )
    {
        var response = await SendAsync(
            api.GetAppDetailsAsync(
                appIds: appId,
                language: language,
                cancellationToken: cancellationToken
            )
        );

        return MapApp(appId, response?.GetValueOrDefault(appId));
    }

    private static string ResolveLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return DefaultLanguage;
        }

        var prefix = languageCode.Split('-')[0].ToLowerInvariant();
        return Languages.GetValueOrDefault(prefix) ?? DefaultLanguage;
    }

    private static async Task<T?> SendAsync<T>(Task<ApiResponse<T>> request)
    {
        using var response = await request;

        if (response.IsSuccessStatusCode)
        {
            return response.Content;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new MediaMetadataDatabaseRateLimitExceededException("Steam", null);
        }

        if (response.Error is not null)
        {
            throw response.Error;
        }

        throw new HttpRequestException($"Steam returned {response.StatusCode}.");
    }

    private static MediaMetadata? MapApp(string appId, SteamAppDetailsResponse? details)
    {
        if (details?.Success != true || details.Data is null)
        {
            return null;
        }

        var data = details.Data;

        if (!string.Equals(data.Type, "game", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(data.Name))
        {
            return null;
        }

        var genres = data
            .Genres?.Select(genre => genre.Description)
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .ToList();
        var genre = genres is null || genres.Count == 0 ? null : string.Join(", ", genres);

        return new MediaMetadata(
            Title: data.Name,
            Description: data.ShortDescription,
            Genre: genre,
            CoverUrl: data.HeaderImage,
            DatabaseUrl: $"https://store.steampowered.com/app/{appId}"
        );
    }
}
