using System.Text.Json;
using System.Text.RegularExpressions;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.NfoDatabases.Srrdb.Api;
using NfoReleaseNfo = Bearcat.Abstractions.NfoDatabase.ReleaseNfo;

namespace Bearcat.NfoDatabases.Srrdb;

public partial class SrrdbNfoDatabase(SrrdbClient client) : INfoDatabase, INfoProvider
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\.{2,}")]
    private static partial Regex MultipleDotsRegex();

    public string Name => "srrDB";

    public int ResolutionPriority => 100;

    public IReadOnlyList<string> ConfigurationKeys => [];

    public async Task<ReleaseInfo?> GetReleaseInfoAsync(
        INfoDatabaseConfig config,
        string dirname,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedDirname = NormalizeDirname(dirname);
        if (string.IsNullOrWhiteSpace(normalizedDirname))
        {
            return null;
        }

        var detailsTask = client.GetDetailsAsync(normalizedDirname, cancellationToken);
        var imdbTask = client.GetImdbAsync(normalizedDirname, cancellationToken);

        await Task.WhenAll(detailsTask, imdbTask);

        var details = await detailsTask;
        if (details is null || string.IsNullOrWhiteSpace(details.Name))
        {
            return null;
        }

        var imdb = (await imdbTask)?.Releases?.FirstOrDefault(release =>
            !string.IsNullOrWhiteSpace(release.Imdb)
        );

        return new ReleaseInfo(
            ReleaseName: details.Name,
            ReleaseDatabaseUrl: GetReleaseDatabaseUrl(details.Name),
            Size: GetSize(details),
            VideoType: null,
            AudioType: null,
            Genre: null,
            Description: null,
            CoverUrl: null,
            ExternalInfos: MapExternalInfos(imdb)
        );
    }

    public async Task<NfoReleaseNfo?> GetReleaseNfoAsync(
        INfoDatabaseConfig config,
        string dirname,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedDirname = NormalizeDirname(dirname);
        if (string.IsNullOrWhiteSpace(normalizedDirname))
        {
            return null;
        }

        var response = await client.GetNfoAsync(normalizedDirname, cancellationToken);
        var nfoLink = response?.NfoLink?.FirstOrDefault(link => !string.IsNullOrWhiteSpace(link));
        if (string.IsNullOrWhiteSpace(nfoLink))
        {
            return null;
        }

        var fileName =
            response?.Nfo?.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? GetFileNameFromUrl(nfoLink)
            ?? $"{normalizedDirname}.nfo";
        var content = await client.DownloadNfoAsync(nfoLink, cancellationToken);

        return string.IsNullOrWhiteSpace(content) ? null : new NfoReleaseNfo(fileName, content);
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>());
    }

    public INfoDatabaseConfig DeserializeConfig(string serializedConfig)
    {
        return new SrrdbConfig();
    }

    private static ReleaseInfoSize? GetSize(SrrdbDetailsResponse details)
    {
        var totalBytes = details.ArchivedFiles?.Sum(file => file.Size ?? 0) ?? 0;
        if (totalBytes <= 0)
        {
            totalBytes = details.Files?.Sum(file => file.Size ?? 0) ?? 0;
        }

        if (totalBytes <= 0)
        {
            return null;
        }

        var megabytes = (int)Math.Round(totalBytes / 1024d / 1024d);
        return new ReleaseInfoSize(megabytes, "MB");
    }

    private static IReadOnlyList<ExternalInfo> MapExternalInfos(SrrdbImdbReleaseResponse? imdb)
    {
        if (imdb is null || string.IsNullOrWhiteSpace(imdb.Imdb))
        {
            return [];
        }

        return
        [
            new ExternalInfo(
                ExternalInfoType.Movie,
                imdb.Title,
                [new Url(UrlType.Imdb, $"https://www.imdb.com/title/tt{imdb.Imdb.TrimStart('t')}")]
            ),
        ];
    }

    private static string GetReleaseDatabaseUrl(string releaseName)
    {
        return $"https://www.srrdb.com/release/details/{Uri.EscapeDataString(releaseName)}";
    }

    private static string? GetFileNameFromUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Path.GetFileName(uri.LocalPath)
            : null;
    }

    private static string NormalizeDirname(string dirname)
    {
        var normalized = WhitespaceRegex().Replace(dirname.Trim(), ".");
        normalized = MultipleDotsRegex().Replace(normalized, ".");
        return normalized.Trim('.');
    }
}
