using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ImportLinkCrypterContainers.Preview;

public class LinkCrypterImportMirrorSplitterTest
{
    private const string FirstEpisode = "Show.S01E01.German.1080p.WEB.x264-GRP";
    private const string SecondEpisode = "Show.S01E02.German.1080p.WEB.x264-GRP";

    [Test]
    public void SplitByArchiveBaseName_SeasonContainer_ReturnsMirrorsWithOnlyTheFilesOfEachEpisode()
    {
        // Arrange
        List<LinkCrypterImportMirror> mirrors =
        [
            CreateMirror(
                mirrorNumber: 2,
                fileNames: [$"{SecondEpisode}.rar", $"{FirstEpisode}.rar", $"{FirstEpisode}.r00"]
            ),
            CreateMirror(
                mirrorNumber: 1,
                fileNames:
                [
                    $"{FirstEpisode}.part1.rar",
                    $"{FirstEpisode}.part2.rar",
                    $"{SecondEpisode}.part1.rar",
                    $"{SecondEpisode}.part2.rar",
                ]
            ),
            CreateMirror(mirrorNumber: 3, fileNames: [$"{FirstEpisode}.part1.rar", null]),
        ];

        // Act
        var mirrorsOfArchives = LinkCrypterImportMirrorSplitter.SplitByArchiveBaseName(mirrors);

        // Assert
        mirrorsOfArchives
            .Select(mirrorsOfArchive => mirrorsOfArchive.ArchiveBaseName)
            .ShouldBe([FirstEpisode, SecondEpisode]);
        GetMirrorsWithFileNames(mirrorsOfArchives[0])
            .ShouldBe([
                (1, null, $"{FirstEpisode}.part1.rar, {FirstEpisode}.part2.rar"),
                (2, null, $"{FirstEpisode}.rar, {FirstEpisode}.r00"),
            ]);
        GetMirrorsWithFileNames(mirrorsOfArchives[1])
            .ShouldBe([
                (1, null, $"{SecondEpisode}.part1.rar, {SecondEpisode}.part2.rar"),
                (2, null, $"{SecondEpisode}.rar"),
            ]);
    }

    [Test]
    public void SplitByArchiveBaseName_OneArchivePerMirror_ReturnsNoArchives()
    {
        // Arrange
        List<LinkCrypterImportMirror> mirrors =
        [
            CreateMirror(
                mirrorNumber: 1,
                fileNames: [$"{FirstEpisode}.part1.rar", $"{FirstEpisode}.part2.rar"]
            ),
            CreateMirror(mirrorNumber: 2, fileNames: [$"{FirstEpisode}.rar"]),
            CreateMirror(mirrorNumber: 3, fileNames: [$"{SecondEpisode}.mkv", "Sample.mkv"]),
        ];

        // Act
        var mirrorsOfArchives = LinkCrypterImportMirrorSplitter.SplitByArchiveBaseName(mirrors);

        // Assert
        mirrorsOfArchives.ShouldBeEmpty();
    }

    private static LinkCrypterImportMirror CreateMirror(
        int mirrorNumber,
        IReadOnlyList<string?> fileNames
    )
    {
        return new LinkCrypterImportMirror(
            MirrorNumber: mirrorNumber,
            HosterClassName: "Hoster",
            Files: fileNames
                .Select(
                    (fileName, index) =>
                        new LinkCrypterImportMirrorFile(
                            Link: $"https://hoster.test/{mirrorNumber}/{index}",
                            OwnFormatLink: $"https://hoster.test/{mirrorNumber}/{index}",
                            IsOnline: fileName is not null,
                            FileName: fileName,
                            SizeBytes: 100
                        )
                )
                .ToList(),
            Decision: LinkCrypterImportMirrorDecision.Adopt,
            UploadByBearcatReason: null
        );
    }

    private static List<(
        int MirrorNumber,
        LinkCrypterImportMirrorDecision? Decision,
        string FileNames
    )> GetMirrorsWithFileNames(LinkCrypterImportMirrorsOfArchive mirrorsOfArchive)
    {
        return mirrorsOfArchive
            .Mirrors.Select(mirror =>
                (
                    mirror.MirrorNumber,
                    mirror.Decision,
                    string.Join(", ", mirror.Files.Select(file => file.FileName))
                )
            )
            .ToList();
    }
}
