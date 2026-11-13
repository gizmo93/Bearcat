using Bearcat.Domain.UseCases.ManageReleases.Parsers;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageReleases;

public class MediaDatabaseReferenceParserTest
{
    [TestCase("tt0089153", ExternalIdentifierType.Imdb, "tt0089153")]
    [TestCase(" TT0089153 ", ExternalIdentifierType.Imdb, "tt0089153")]
    [TestCase("https://www.imdb.com/title/tt0089153/", ExternalIdentifierType.Imdb, "tt0089153")]
    [TestCase(
        "https://m.imdb.com/de/title/tt14039476/?ref_=nv",
        ExternalIdentifierType.Imdb,
        "tt14039476"
    )]
    [TestCase(
        "https://www.themoviedb.org/movie/12775-flesh-blood?language=de",
        ExternalIdentifierType.Tmdb,
        "movie/12775"
    )]
    [TestCase("https://www.themoviedb.org/tv/1399", ExternalIdentifierType.Tmdb, "tv/1399")]
    [TestCase("https://www.thetvdb.com/series/bodies", ExternalIdentifierType.Tvdb, "bodies")]
    [TestCase(
        "https://www.thetvdb.com/dereferrer/series/421249",
        ExternalIdentifierType.Tvdb,
        "421249"
    )]
    [TestCase(
        "https://store.steampowered.com/app/1172710/Dune_Awakening/",
        ExternalIdentifierType.Steam,
        "1172710"
    )]
    [TestCase("https://steamdb.info/app/1172710/", ExternalIdentifierType.Steam, "1172710")]
    public void Parse_SupportedReference_ReturnsTypeAndValue(
        string input,
        ExternalIdentifierType expectedType,
        string expectedValue
    )
    {
        // Act
        var reference = MediaDatabaseReferenceParser.Parse(input);

        // Assert
        reference.ShouldBe(new MediaDatabaseReference(expectedType, expectedValue));
    }

    [TestCase(null)]
    [TestCase("   ")]
    [TestCase("Flesh and Blood 1985")]
    [TestCase("https://www.themoviedb.org/person/3")]
    [TestCase("https://example.com/title/tt0089153")]
    [TestCase("https://steamcommunity.com/app/1172710/")]
    public void Parse_UnsupportedInput_ReturnsNull(string? input)
    {
        // Act
        var reference = MediaDatabaseReferenceParser.Parse(input);

        // Assert
        reference.ShouldBeNull();
    }
}
