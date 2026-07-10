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
}
