using Bearcat.Website.Pages.ManagePostedLocations;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManagePostedLocations;

public class PostedLocationDisplayTest
{
    [Test]
    public void FromUrl_UrlWithWwwPathAndQuery_ReturnsDomainWithoutWwwAndPathWithQuery()
    {
        // Act
        var display = PostedLocationDisplay.FromUrl(
            "https://www.forum.example/threads/release.123/?page=2"
        );

        // Assert
        display.ShouldBe(
            new PostedLocationDisplay("F", "forum.example", "/threads/release.123/?page=2")
        );
    }

    [Test]
    public void FromUrl_UrlWithoutPath_ReturnsNoSubtitle()
    {
        // Act
        var display = PostedLocationDisplay.FromUrl("https://forum.example");

        // Assert
        display.ShouldBe(new PostedLocationDisplay("F", "forum.example", null));
    }

    [Test]
    public void FromUrl_NoAbsoluteUrl_ReturnsRawValueAsTitle()
    {
        // Act
        var display = PostedLocationDisplay.FromUrl("posted in some forum");

        // Assert
        display.ShouldBe(new PostedLocationDisplay("P", "posted in some forum", null));
    }
}
