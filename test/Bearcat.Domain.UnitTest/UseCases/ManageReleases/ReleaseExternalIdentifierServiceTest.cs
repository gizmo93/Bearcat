using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageReleases;

public class ReleaseExternalIdentifierServiceTest
{
    [Test]
    public void SyncImdbIds_ReplacesIdentifiersFromSameSource()
    {
        var release = new Release
        {
            ExternalIdentifiers =
            [
                new ReleaseExternalIdentifier
                {
                    Type = ExternalIdentifierType.Imdb,
                    Value = "tt1111111",
                    Source = ExternalIdentifierSource.Nfo,
                },
                new ReleaseExternalIdentifier
                {
                    Type = ExternalIdentifierType.Imdb,
                    Value = "tt2222222",
                    Source = ExternalIdentifierSource.Srrdb,
                },
            ],
        };

        ReleaseExternalIdentifierService.SyncImdbIds(
            release,
            ExternalIdentifierSource.Nfo,
            ["https://www.imdb.com/title/TT3333333/"]
        );

        release.ExternalIdentifiers.Count.ShouldBe(2);
        release.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Value == "tt3333333" && identifier.Source == ExternalIdentifierSource.Nfo
        );
        release.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Value == "tt2222222" && identifier.Source == ExternalIdentifierSource.Srrdb
        );
    }

    [Test]
    public void SyncSteamAppIds_StoreUrl_AddsSteamIdentifier()
    {
        // Arrange
        var release = new Release();

        // Act
        ReleaseExternalIdentifierService.SyncSteamAppIds(
            release,
            ExternalIdentifierSource.Xrel,
            ["https://store.steampowered.com/app/1172710/Dune_Awakening/"]
        );

        // Assert
        release.ExternalIdentifiers.Count.ShouldBe(1);
        release.ExternalIdentifiers[0].Type.ShouldBe(ExternalIdentifierType.Steam);
        release.ExternalIdentifiers[0].Value.ShouldBe("1172710");
        release.ExternalIdentifiers[0].Source.ShouldBe(ExternalIdentifierSource.Xrel);
    }

    [Test]
    public void SyncSteamAppIds_ReplacesIdentifiersFromSameSource()
    {
        // Arrange
        var release = new Release
        {
            ExternalIdentifiers =
            [
                new ReleaseExternalIdentifier
                {
                    Type = ExternalIdentifierType.Steam,
                    Value = "111111",
                    Source = ExternalIdentifierSource.Nfo,
                },
                new ReleaseExternalIdentifier
                {
                    Type = ExternalIdentifierType.Steam,
                    Value = "222222",
                    Source = ExternalIdentifierSource.Xrel,
                },
            ],
        };

        // Act
        ReleaseExternalIdentifierService.SyncSteamAppIds(
            release,
            ExternalIdentifierSource.Nfo,
            ["https://store.steampowered.com/app/333333/"]
        );

        // Assert
        release.ExternalIdentifiers.Count.ShouldBe(2);
        release.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Value == "333333" && identifier.Source == ExternalIdentifierSource.Nfo
        );
        release.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Value == "222222" && identifier.Source == ExternalIdentifierSource.Xrel
        );
    }

    [Test]
    public void SyncSteamAppIds_LeavesImdbIdentifiersOfSameSourceUntouched()
    {
        // Arrange
        var release = new Release
        {
            ExternalIdentifiers =
            [
                new ReleaseExternalIdentifier
                {
                    Type = ExternalIdentifierType.Imdb,
                    Value = "tt1111111",
                    Source = ExternalIdentifierSource.Nfo,
                },
            ],
        };

        // Act
        ReleaseExternalIdentifierService.SyncSteamAppIds(
            release,
            ExternalIdentifierSource.Nfo,
            ["https://store.steampowered.com/app/1172710/"]
        );

        // Assert
        release.ExternalIdentifiers.Count.ShouldBe(2);
        release.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Type == ExternalIdentifierType.Imdb && identifier.Value == "tt1111111"
        );
        release.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Type == ExternalIdentifierType.Steam && identifier.Value == "1172710"
        );
    }

    [Test]
    public void SyncImdbIds_LeavesSteamIdentifiersOfSameSourceUntouched()
    {
        // Arrange
        var release = new Release
        {
            ExternalIdentifiers =
            [
                new ReleaseExternalIdentifier
                {
                    Type = ExternalIdentifierType.Steam,
                    Value = "1172710",
                    Source = ExternalIdentifierSource.Nfo,
                },
            ],
        };

        // Act
        ReleaseExternalIdentifierService.SyncImdbIds(
            release,
            ExternalIdentifierSource.Nfo,
            ["https://www.imdb.com/title/tt1111111/"]
        );

        // Assert
        release.ExternalIdentifiers.Count.ShouldBe(2);
        release.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Type == ExternalIdentifierType.Steam && identifier.Value == "1172710"
        );
        release.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Type == ExternalIdentifierType.Imdb && identifier.Value == "tt1111111"
        );
    }
}
