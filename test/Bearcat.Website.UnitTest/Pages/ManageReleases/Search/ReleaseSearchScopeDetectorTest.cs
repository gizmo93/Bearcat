using Bearcat.Website.Pages.ManageReleases.Search;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases.Search;

public class ReleaseSearchScopeDetectorTest
{
    [TestCase("https://example.com/thread/123")]
    [TestCase("http://example.com/file")]
    [TestCase("HTTPS://EXAMPLE.COM")]
    [TestCase("  https://example.com/thread/123  ")]
    public void GetMatchingScopes_Url_ReturnsDownloadLinkAndPostedLocation(string text)
    {
        // Act
        var scopes = ReleaseSearchScopeDetector.GetMatchingScopes(text);

        // Assert
        scopes.ShouldBe([ReleaseSearchScope.DownloadLink, ReleaseSearchScope.PostedLocation]);
    }

    [TestCase("Some.Release.2024.rar")]
    [TestCase("Some.Release.2024.7z")]
    [TestCase("Some.Release.2024.zip")]
    [TestCase("Some.Release.2024.r00")]
    [TestCase("Some.Release.2024.R17")]
    [TestCase("Some.Release.2024.7z.001")]
    [TestCase("Some.Release.2024.part1.rar")]
    [TestCase("Some.Release.2024.PART12.exe")]
    [TestCase("archive.RAR")]
    [TestCase("  archive.rar  ")]
    public void GetMatchingScopes_ArchiveFileName_ReturnsArchiveFile(string text)
    {
        // Act
        var scopes = ReleaseSearchScopeDetector.GetMatchingScopes(text);

        // Assert
        scopes.ShouldBe([ReleaseSearchScope.ArchiveFile]);
    }

    [TestCase("123")]
    [TestCase("#123")]
    [TestCase("  #7  ")]
    public void GetMatchingScopes_UploadId_ReturnsUploadId(string text)
    {
        // Act
        var scopes = ReleaseSearchScopeDetector.GetMatchingScopes(text);

        // Assert
        scopes.ShouldBe([ReleaseSearchScope.UploadId]);
    }

    [Test]
    public void GetMatchingScopes_UrlEndingWithArchiveExtension_ReturnsUrlScopesAndArchiveFile()
    {
        // Act
        var scopes = ReleaseSearchScopeDetector.GetMatchingScopes(
            "https://example.com/Some.Release.part1.rar"
        );

        // Assert
        scopes.ShouldBe([
            ReleaseSearchScope.DownloadLink,
            ReleaseSearchScope.PostedLocation,
            ReleaseSearchScope.ArchiveFile,
        ]);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Some Release 2024")]
    [TestCase("Some.Release.2024")]
    [TestCase("Some Release.rar")]
    [TestCase("Some.Release.r1")]
    [TestCase("Some.Release.01")]
    [TestCase("Some.Release.rarx")]
    [TestCase("ftp://example.com/file")]
    [TestCase("example.com/thread")]
    [TestCase("#")]
    [TestCase("##123")]
    [TestCase("12a")]
    [TestCase("-12")]
    [TestCase("1 2")]
    [TestCase("99999999999")]
    public void GetMatchingScopes_NoPattern_ReturnsEmpty(string text)
    {
        // Act
        var scopes = ReleaseSearchScopeDetector.GetMatchingScopes(text);

        // Assert
        scopes.ShouldBeEmpty();
    }
}
