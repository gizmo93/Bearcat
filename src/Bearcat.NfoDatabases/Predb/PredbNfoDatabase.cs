using System.Text.Json;
using System.Text.RegularExpressions;
using Bearcat.Abstractions.NfoDatabase;
using NfoReleaseNfo = Bearcat.Abstractions.NfoDatabase.ReleaseNfo;

namespace Bearcat.NfoDatabases.Predb;

public partial class PredbNfoDatabase(PredbClient client) : INfoDatabase, INfoProvider
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\.{2,}")]
    private static partial Regex MultipleDotsRegex();

    public string Name => "PreDB";

    public int ResolutionPriority => 200;

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

        var release = await client.GetPreAsync(normalizedDirname, cancellationToken);
        if (release is null || string.IsNullOrWhiteSpace(release.Release))
        {
            return null;
        }

        return new ReleaseInfo(
            ReleaseName: release.Release,
            ReleaseDatabaseUrl: GetReleaseDatabaseUrl(release.Release),
            Size: GetSize(release.Size),
            VideoType: null,
            AudioType: null,
            Genre: string.IsNullOrWhiteSpace(release.Genre) ? null : release.Genre,
            Description: null,
            CoverUrl: null,
            ExternalInfos: [],
            ContentKind: MapContentKind(release.Section)
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
        if (response is null || string.IsNullOrWhiteSpace(response.Nfo))
        {
            return null;
        }

        var fileName = GetFileNameFromUrl(response.Nfo) ?? $"{normalizedDirname}.nfo";
        var content = await client.DownloadNfoAsync(response.Nfo, cancellationToken);

        return string.IsNullOrWhiteSpace(content) ? null : new NfoReleaseNfo(fileName, content);
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(new Dictionary<string, string>());
    }

    public INfoDatabaseConfig DeserializeConfig(string serializedConfig)
    {
        return new PredbConfig();
    }

    private static ExternalInfoType? MapContentKind(string? section)
    {
        return section?.StartsWith("GAMES", StringComparison.OrdinalIgnoreCase) == true
            ? ExternalInfoType.Game
            : null;
    }

    private static ReleaseInfoSize? GetSize(double? megabytes)
    {
        return megabytes is > 0
            ? new ReleaseInfoSize((int)Math.Round(megabytes.Value), "MB")
            : null;
    }

    private static string GetReleaseDatabaseUrl(string releaseName)
    {
        return $"https://predb.net/rls/{Uri.EscapeDataString(releaseName)}";
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
