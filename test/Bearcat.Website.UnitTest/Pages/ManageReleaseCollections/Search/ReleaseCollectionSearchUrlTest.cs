using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleaseCollections.Search;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleaseCollections.Search;

public class ReleaseCollectionSearchUrlTest
{
    [Test]
    public void Parse_NoParameters_ReturnsEmptyQueryWithNameAscending()
    {
        // Act
        var state = ReleaseCollectionSearchUrl.Parse(new ReleaseCollectionSearchUrlValues());

        // Assert
        state.Query.ShouldBe(
            new ReleaseCollectionSearchQuery(
                SortOrder: ReleaseCollectionSearchSortOrder.NameAscending
            )
        );
        state.PageIndex.ShouldBe(0);
    }

    [TestCase("Popularity")]
    [TestCase("99")]
    public void Parse_SortOrderInvalid_ReturnsNameAscending(string sortOrder)
    {
        // Act
        var state = ReleaseCollectionSearchUrl.Parse(
            new ReleaseCollectionSearchUrlValues(SortOrder: sortOrder)
        );

        // Assert
        state.Query.SortOrder.ShouldBe(ReleaseCollectionSearchSortOrder.NameAscending);
    }

    [Test]
    public void Parse_LanguageNone_ReturnsEmptyPrimaryLanguageCode()
    {
        // Act
        var state = ReleaseCollectionSearchUrl.Parse(
            new ReleaseCollectionSearchUrlValues(Language: "none")
        );

        // Assert
        state.Query.PrimaryLanguageCode.ShouldBe(string.Empty);
    }

    [Test]
    public void Build_DefaultSortOrderAndFirstPage_OmitsSortAndPageParameters()
    {
        // Arrange
        var query = new ReleaseCollectionSearchQuery(SearchTerm: "series");

        // Act
        var url = ReleaseCollectionSearchUrl.Build(query, page: 1, pageSize: 10);

        // Assert
        url.ShouldBe("/release-collections?q=series");
    }

    [Test]
    public void Build_ParsedUrlWithAllParameters_KeepsAllParameters()
    {
        // Arrange
        var state = ReleaseCollectionSearchUrl.Parse(
            new ReleaseCollectionSearchUrlValues(
                SearchTerm: "series",
                ReleaseContentType: "TvShowEpisode",
                Language: "none",
                OnlineState: "Offline",
                ReleaseGroupId: 3,
                SortOrder: "CreatedAtDescending",
                Page: 2,
                PageSize: 20
            )
        );

        // Act
        var url = ReleaseCollectionSearchUrl.Build(state.Query, page: 2, state.PageSize);

        // Assert
        state.Query.ShouldBe(
            new ReleaseCollectionSearchQuery(
                SearchTerm: "series",
                ReleaseContentType: ReleaseContentType.TvShowEpisode,
                ReleaseGroupId: 3,
                OnlineState: OnlineState.Offline,
                PrimaryLanguageCode: string.Empty,
                SortOrder: ReleaseCollectionSearchSortOrder.CreatedAtDescending
            )
        );
        url.ShouldBe(
            "/release-collections?q=series&content=TvShowEpisode&lang=none&state=Offline&group=3&sort=CreatedAtDescending&page=2&size=20"
        );
    }

    private sealed record ReleaseCollectionSearchUrlValues(
        string? SearchTerm = null,
        string? ReleaseContentType = null,
        string? Language = null,
        string? OnlineState = null,
        int? ReleaseGroupId = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IReleaseCollectionSearchUrlValues;
}
