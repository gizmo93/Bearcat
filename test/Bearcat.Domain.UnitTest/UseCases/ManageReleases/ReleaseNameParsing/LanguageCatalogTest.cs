using Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageReleases.ReleaseNameParsing;

public class LanguageCatalogTest
{
    [TestCase("de", "German")]
    [TestCase("en", "English")]
    [TestCase("deu", "German")]
    [TestCase("German", "German")]
    [TestCase("Deutsch", "German")]
    [TestCase("ger", "German")]
    [TestCase("de-DE", "German")]
    [TestCase("pt-BR", "Portuguese")]
    public void Resolve_KnownCodeOrName_ReturnsCanonicalEnglishName(
        string value,
        string expected
    )
    {
        // Act & Assert
        LanguageCatalog.Resolve(value).ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("zz")]
    [TestCase("notalanguage")]
    public void Resolve_UnknownValue_ReturnsNull(string? value)
    {
        // Act & Assert
        LanguageCatalog.Resolve(value).ShouldBeNull();
    }

    [Test]
    public void TryResolveName_TwoLetterCode_IsNotMatchedAsToken()
    {
        // Act
        var matched = LanguageCatalog.TryResolveName("de", out _);

        // Assert
        matched.ShouldBeFalse();
    }

    [Test]
    public void TryResolveName_FullName_IsMatched()
    {
        // Act
        var matched = LanguageCatalog.TryResolveName("German", out var canonical);

        // Assert
        matched.ShouldBeTrue();
        canonical.ShouldBe("German");
    }
}
