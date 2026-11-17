using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared;

public class ReleaseLanguageResolverTest
{
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
