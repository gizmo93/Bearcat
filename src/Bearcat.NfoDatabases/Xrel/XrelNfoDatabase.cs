using System.Text.Json;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Xrel.Api;

namespace Bearcat.NfoDatabases.Xrel;

public class XrelNfoDatabase(XrelClient client) : INfoDatabase
{
    public string Name => "xREL";

    public IReadOnlyList<string> ConfigurationKeys => [];

    public async Task<ReleaseInfo?> GetReleaseInfoAsync(
        INfoDatabaseConfig config,
        string dirname,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(dirname))
        {
            return null;
        }

        var release = await client.GetReleaseInfoAsync(dirname, cancellationToken);
        if (release is null || string.IsNullOrWhiteSpace(release.Dirname))
        {
            return null;
        }

        return new ReleaseInfo(
            ReleaseName: release.Dirname,
            ReleaseDatabaseUrl: NormalizeXrelUrl(release.LinkHref),
            Size: release.Size is null
                ? null
                : new ReleaseInfoSize(release.Size.Number, release.Size.Unit),
            VideoType: release.VideoType,
            AudioType: release.AudioType,
            ExternalInfos: MapExternalInfos(release.ExtInfo)
        );
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>());
    }

    public INfoDatabaseConfig DeserializeConfig(string serializedConfig)
    {
        return new XrelConfig();
    }

    private static IReadOnlyList<ExternalInfo> MapExternalInfos(XrelExternalInfo? externalInfo)
    {
        if (externalInfo is null)
        {
            return [];
        }

        var urls = new List<Url>();
        var xrelUrl = NormalizeXrelUrl(externalInfo.LinkHref);
        if (!string.IsNullOrWhiteSpace(xrelUrl))
        {
            urls.Add(new Url(UrlType.Other, xrelUrl));
        }

        if (externalInfo.Uris is not null)
        {
            urls.AddRange(externalInfo.Uris.Select(MapUri).Where(url => url is not null)!);
        }

        return
        [
            new ExternalInfo(
                Type: MapExternalInfoType(externalInfo.Type),
                Title: externalInfo.Title,
                Urls: urls.DistinctBy(url => url.Value).ToList()
            ),
        ];
    }

    private static Url? MapUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        const string imdbPrefix = "imdb:";
        if (uri.StartsWith(imdbPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var imdbId = uri[imdbPrefix.Length..].Trim();
            return string.IsNullOrWhiteSpace(imdbId)
                ? null
                : new Url(UrlType.Imdb, $"https://www.imdb.com/de/title/{imdbId}");
        }

        return new Url(UrlType.Other, uri);
    }

    private static ExternalInfoType MapExternalInfoType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "movie" => ExternalInfoType.Movie,
            "tv" => ExternalInfoType.Tv,
            "game" => ExternalInfoType.Game,
            "console" => ExternalInfoType.Console,
            "software" => ExternalInfoType.Software,
            "xxx" => ExternalInfoType.Xxx,
            _ => ExternalInfoType.Other,
        };
    }

    private static string? NormalizeXrelUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return $"https:{url}";
        }

        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            return $"https://www.xrel.to{url}";
        }

        return url;
    }
}
