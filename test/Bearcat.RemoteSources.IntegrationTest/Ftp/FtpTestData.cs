using System.Text;

namespace Bearcat.RemoteSources.IntegrationTest.Ftp;

public static class FtpTestData
{
    public const string CompleteMarkerFolder = "[TEST] - ( 5M 3F - COMPLETE ) - [TEST]";

    public const string ReleaseOneFolder = "Release.One-GROUP";

    public const string ReleaseTwoFolder = "Release.Two-GROUP";

    public const string LargeFileRelativePath = "release.one-group.rar";

    public const string NfoFileRelativePath = "release.one-group.nfo";

    public const string SymlinkedReleaseFolder = "(incomplete)-Release.Two-GROUP";

    public const string SymlinkedNfoRelativePath = "release.one-group.link.nfo";

    public const string SymlinkedSubfolder = "Linked.Subs";

    public const string SymlinkedSubfolderTarget = "Subs";

    public static IReadOnlyDictionary<string, byte[]> Files { get; } = CreateFiles();

    public static IReadOnlyDictionary<string, byte[]> GetReleaseFiles(string releaseFolder)
    {
        var prefix = releaseFolder + "/";

        return Files
            .Where(file => file.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(file => file.Key[prefix.Length..], file => file.Value);
    }

    private static Dictionary<string, byte[]> CreateFiles()
    {
        var random = new Random(4711);

        return new Dictionary<string, byte[]>
        {
            ["welcome.txt"] = Encoding.UTF8.GetBytes("Welcome to the Bearcat test FTP server"),
            [$"{ReleaseOneFolder}/{NfoFileRelativePath}"] = Encoding.UTF8.GetBytes(
                "Release One NFO"
            ),
            [$"{ReleaseOneFolder}/release.one-group.sfv"] = Encoding.UTF8.GetBytes(
                "release.one-group.rar 12345678"
            ),
            [$"{ReleaseOneFolder}/{LargeFileRelativePath}"] = CreateRandomBytes(
                random,
                (5 * 1024 * 1024) + 123
            ),
            [$"{ReleaseOneFolder}/release.one-group.r00"] = CreateRandomBytes(random, 1024 * 1024),
            [$"{ReleaseOneFolder}/Sample/release.one-group.sample.mkv"] = CreateRandomBytes(
                random,
                300_000
            ),
            [$"{ReleaseOneFolder}/Subs/Nested/empty.srt"] = [],
            [$"{ReleaseTwoFolder}/release.two-group.nfo"] = Encoding.UTF8.GetBytes(
                "Release Two NFO"
            ),
            [$"{ReleaseTwoFolder}/release.two-group.rar"] = CreateRandomBytes(random, 200_000),
        };
    }

    private static byte[] CreateRandomBytes(Random random, int length)
    {
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }
}
