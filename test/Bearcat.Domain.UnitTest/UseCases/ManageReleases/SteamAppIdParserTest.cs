using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Parsers;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageReleases;

public class SteamAppIdParserTest
{
    [Test]
    public void ExtractAll_NfoContainsStoreUrl_ReturnsAppId()
    {
        // Arrange
        const string content = """
            ..................................................
            Store ... https://store.steampowered.com/app/1172710/Dune_Awakening/
            ..................................................
            """;

        // Act
        var appIds = SteamAppIdParser.ExtractAll(content);

        // Assert
        appIds.ShouldBe(["1172710"]);
    }

    [Test]
    public void ExtractAll_BareUrl_ReturnsAppId()
    {
        // Arrange
        const string value = "https://store.steampowered.com/app/440";

        // Act
        var appIds = SteamAppIdParser.ExtractAll(value);

        // Assert
        appIds.ShouldBe(["440"]);
    }

    [Test]
    public void ExtractAll_RepeatedAppId_ReturnsDistinctValues()
    {
        // Arrange
        const string content =
            "https://store.steampowered.com/app/1172710/Dune_Awakening/ "
            + "https://store.steampowered.com/app/1172710/ "
            + "https://store.steampowered.com/app/440/";

        // Act
        var appIds = SteamAppIdParser.ExtractAll(content);

        // Assert
        appIds.ShouldBe(["1172710", "440"]);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Bearcat.Release.2026-GRP without any links")]
    [TestCase("https://www.imdb.com/title/tt1234567/")]
    [TestCase("https://steamcommunity.com/app/1172710/")]
    [TestCase("https://steamdb.info/app/1172710/")]
    public void ExtractAll_WithoutStoreUrl_ReturnsEmptyList(string? value)
    {
        // Act
        var appIds = SteamAppIdParser.ExtractAll(value);

        // Assert
        appIds.ShouldBeEmpty();
    }
}
