using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared;

public class ReleaseLanguageResolverTest
{
    [Test]
    public void Resolve_UserLanguageCodeSet_WinsOverClassification()
    {
        // Arrange
        var release = ReleaseWith(primaryLanguageCode: "fr", classificationLanguage: "German");

        // Act
        var language = ReleaseLanguageResolver.Resolve(release);

        // Assert
        language.ShouldBe("French");
    }

    [Test]
    public void Resolve_NoUserLanguageCode_FallsBackToClassification()
    {
        // Arrange
        var release = ReleaseWith(primaryLanguageCode: null, classificationLanguage: "German");

        // Act
        var language = ReleaseLanguageResolver.Resolve(release);

        // Assert
        language.ShouldBe("German");
    }

    [Test]
    public void Resolve_UnknownUserLanguageCode_FallsBackToClassification()
    {
        // Arrange
        var release = ReleaseWith(primaryLanguageCode: "zzz", classificationLanguage: "German");

        // Act
        var language = ReleaseLanguageResolver.Resolve(release);

        // Assert
        language.ShouldBe("German");
    }

    [Test]
    public void Resolve_NoClassification_ReturnsNull()
    {
        // Arrange
        var release = new Release { Name = "Some.Movie.2020-GROUP" };

        // Act
        var language = ReleaseLanguageResolver.Resolve(release);

        // Assert
        language.ShouldBeNull();
    }

    [Test]
    public void Resolve_NoUserLanguageCodeAndNoClassificationLanguage_ReturnsNull()
    {
        // Arrange
        var release = ReleaseWith(primaryLanguageCode: null, classificationLanguage: null);

        // Act
        var language = ReleaseLanguageResolver.Resolve(release);

        // Assert
        language.ShouldBeNull();
    }

    private static Release ReleaseWith(string? primaryLanguageCode, string? classificationLanguage)
    {
        return new Release
        {
            Name = "Some.Movie.2020-GROUP",
            PrimaryLanguageCode = primaryLanguageCode,
            Classification = new ReleaseClassification
            {
                Title = "Some Movie",
                PrimaryLanguage = classificationLanguage,
            },
        };
    }
}
