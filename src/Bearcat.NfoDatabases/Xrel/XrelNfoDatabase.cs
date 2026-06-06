using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Xrel.Api;

namespace Bearcat.NfoDatabases.Xrel;

public partial class XrelNfoDatabase(XrelClient client, IHttpClientFactory httpClientFactory)
    : INfoDatabase
{
    public const string CoverHttpClientName = "xrel-cover";

    public string Name => "xREL";

    public int ResolutionPriority => 0;

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

        var normalizedDirname = NormalizeDirname(dirname);
        if (string.IsNullOrWhiteSpace(normalizedDirname))
        {
            return null;
        }

        var release = await client.GetReleaseInfoAsync(normalizedDirname, cancellationToken);
        if (release is not null && !string.IsNullOrWhiteSpace(release.Dirname))
        {
            var externalInfoEnrichment = await GetExternalInfoEnrichmentAsync(
                release.ExtInfo,
                cancellationToken
            );
            return MapReleaseInfo(release, externalInfoEnrichment);
        }

        var p2pRelease = await client.GetP2pReleaseInfoAsync(normalizedDirname, cancellationToken);
        if (p2pRelease is null || string.IsNullOrWhiteSpace(p2pRelease.Dirname))
        {
            return null;
        }

        var p2pExternalInfoEnrichment = await GetExternalInfoEnrichmentAsync(
            p2pRelease.ExtInfo,
            cancellationToken
        );
        return MapReleaseInfo(p2pRelease, p2pExternalInfoEnrichment);
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>());
    }

    public INfoDatabaseConfig DeserializeConfig(string serializedConfig)
    {
        return new XrelConfig();
    }

    private async Task<XrelExternalInfoEnrichment?> GetExternalInfoEnrichmentAsync(
        XrelExternalInfo? externalInfo,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(externalInfo?.Id))
        {
            return null;
        }

        var details = await client.GetExternalInfoDetailsAsync(externalInfo.Id, cancellationToken);

        var media = await client.GetExternalInfoMediaAsync(externalInfo.Id, cancellationToken);
        var mediaCoverUrl = NormalizeXrelUrl(media.FirstOrDefault(IsImageMedia)?.UrlFull);

        var coverUrl = await ResolveCoverUrlAsync(
            detailsCoverUrl: details?.CoverUrl,
            mediaCoverUrl: mediaCoverUrl,
            cancellationToken: cancellationToken
        );

        return new XrelExternalInfoEnrichment(
            Genre: NullIfWhiteSpace(details?.Genre),
            Description: NormalizeDescription(
                details?.Externals?.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.Plot))?.Plot
            ),
            CoverUrl: coverUrl
        );
    }

    private static ReleaseInfo MapReleaseInfo(
        XrelRelease release,
        XrelExternalInfoEnrichment? externalInfoEnrichment
    )
    {
        return new ReleaseInfo(
            ReleaseName: release.Dirname!,
            ReleaseDatabaseUrl: NormalizeXrelUrl(release.LinkHref),
            Size: release.Size is null
                ? null
                : new ReleaseInfoSize(release.Size.Number, release.Size.Unit),
            VideoType: release.VideoType,
            AudioType: release.AudioType,
            Genre: externalInfoEnrichment?.Genre,
            Description: externalInfoEnrichment?.Description,
            CoverUrl: externalInfoEnrichment?.CoverUrl,
            ExternalInfos: MapExternalInfos(release.ExtInfo)
        );
    }

    private static ReleaseInfo MapReleaseInfo(
        XrelP2pRelease release,
        XrelExternalInfoEnrichment? externalInfoEnrichment
    )
    {
        return new ReleaseInfo(
            ReleaseName: release.Dirname!,
            ReleaseDatabaseUrl: NormalizeXrelUrl(release.LinkHref),
            Size: release.SizeMb is null ? null : new ReleaseInfoSize(release.SizeMb, "MB"),
            VideoType: null,
            AudioType: null,
            Genre: externalInfoEnrichment?.Genre,
            Description: externalInfoEnrichment?.Description,
            CoverUrl: externalInfoEnrichment?.CoverUrl,
            ExternalInfos: MapExternalInfos(release.ExtInfo)
        );
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

    private static string? ToFullCoverUrl(string? coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            return null;
        }

        var urlParts = coverUrl.Split('.');
        var fileExtension = urlParts[^1];

        return $"{string.Join('.', urlParts[..^1])}-full.{fileExtension}";
    }

    private async Task<string?> ResolveCoverUrlAsync(
        string? detailsCoverUrl,
        string? mediaCoverUrl,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(detailsCoverUrl))
        {
            return mediaCoverUrl;
        }

        var fullCoverUrl = ToFullCoverUrl(detailsCoverUrl);
        if (
            !string.IsNullOrWhiteSpace(fullCoverUrl)
            && await CoverUrlExistsAsync(fullCoverUrl, cancellationToken)
        )
        {
            return fullCoverUrl;
        }

        return mediaCoverUrl ?? NormalizeXrelUrl(detailsCoverUrl);
    }

    private async Task<bool> CoverUrlExistsAsync(
        string coverUrl,
        CancellationToken cancellationToken
    )
    {
        if (!Uri.TryCreate(coverUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var httpClient = httpClientFactory.CreateClient(CoverHttpClientName);
        return await UrlExistsAsync(
            httpClient: httpClient,
            uri: uri,
            cancellationToken: cancellationToken
        );
    }

    private static async Task<bool> UrlExistsAsync(
        HttpClient httpClient,
        Uri uri,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Bearcat/1.0");
            using var response = await httpClient.SendAsync(
                request: request,
                completionOption: HttpCompletionOption.ResponseHeadersRead,
                cancellationToken: cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
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

    private static bool IsImageMedia(XrelExternalInfoMedia media)
    {
        return media.Type?.Equals("image", StringComparison.OrdinalIgnoreCase) == true
            && !string.IsNullOrWhiteSpace(media.UrlFull);
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

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var normalized = HtmlBreakRegex().Replace(description, "\n");
        normalized = HtmlTagRegex().Replace(normalized, string.Empty);
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = normalized.ReplaceLineEndings("\n").Trim();
        normalized = MultipleEmptyLinesRegex().Replace(normalized, "\n\n");

        return NullIfWhiteSpace(normalized);
    }

    private static string NormalizeDirname(string dirname)
    {
        var normalized = WhitespaceRegex().Replace(dirname.Trim(), ".");
        normalized = MultipleDotsRegex().Replace(normalized, ".");
        return normalized.Trim('.');
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\.{2,}")]
    private static partial Regex MultipleDotsRegex();

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlBreakRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleEmptyLinesRegex();

    private sealed record XrelExternalInfoEnrichment(
        string? Genre,
        string? Description,
        string? CoverUrl
    );
}
