using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Website.Pages.ManageReleases;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases;

public class ReleaseSearchUrlTest
{
    [Test]
    public void Parse_SortOrderMissing_ReturnsCreatedAtDescending()
    {
        // Act
        var state = ReleaseSearchUrl.Parse(new ReleaseSearchUrlValues(SortOrder: null));

        // Assert
        state.Query.SortOrder.ShouldBe(ReleaseSearchSortOrder.CreatedAtDescending);
    }

    [TestCase("Popularity")]
    [TestCase("99")]
    [TestCase("")]
    public void Parse_SortOrderInvalid_ReturnsCreatedAtDescending(string sortOrder)
    {
        // Act
        var state = ReleaseSearchUrl.Parse(new ReleaseSearchUrlValues(SortOrder: sortOrder));

        // Assert
        state.Query.SortOrder.ShouldBe(ReleaseSearchSortOrder.CreatedAtDescending);
    }

    [TestCase("NameAscending", ReleaseSearchSortOrder.NameAscending)]
    [TestCase("uploadspostedatdescending", ReleaseSearchSortOrder.UploadsPostedAtDescending)]
    [TestCase(
        "OfflineUploadConfigCountDescending",
        ReleaseSearchSortOrder.OfflineUploadConfigCountDescending
    )]
    public void Parse_SortOrderValid_ReturnsThatSortOrder(
        string sortOrder,
        ReleaseSearchSortOrder expectedSortOrder
    )
    {
        // Act
        var state = ReleaseSearchUrl.Parse(new ReleaseSearchUrlValues(SortOrder: sortOrder));

        // Assert
        state.Query.SortOrder.ShouldBe(expectedSortOrder);
    }

    [Test]
    public void Build_DefaultSortOrder_OmitsSortParameter()
    {
        // Arrange
        var query = new ReleaseSearchQuery(
            SearchTerm: "release",
            SortOrder: ReleaseSearchSortOrder.CreatedAtDescending
        );

        // Act
        var url = ReleaseSearchUrl.Build(query, page: 1, pageSize: 5);

        // Assert
        url.ShouldBe("/releases?q=release");
    }

    [Test]
    public void Build_NonDefaultSortOrder_AddsSortParameter()
    {
        // Arrange
        var query = new ReleaseSearchQuery(SortOrder: ReleaseSearchSortOrder.NameAscending);

        // Act
        var url = ReleaseSearchUrl.Build(query, page: 1, pageSize: 5);

        // Assert
        url.ShouldBe("/releases?sort=NameAscending");
    }

    [Test]
    public void Build_ParsedUrlWithSortOrder_KeepsSortOrder()
    {
        // Arrange
        var state = ReleaseSearchUrl.Parse(
            new ReleaseSearchUrlValues(SortOrder: "UploadsPostedAtDescending")
        );

        // Act
        var url = ReleaseSearchUrl.Build(state.Query, page: 2, pageSize: 5);

        // Assert
        url.ShouldBe("/releases?sort=UploadsPostedAtDescending&page=2");
    }

    private sealed record ReleaseSearchUrlValues(string? SortOrder) : IReleaseSearchUrlValues
    {
        public string? SearchTerm => null;
        public string? ReleaseType => null;
        public string? ReleaseContentType => null;
        public string? Language => null;
        public string? OnlineState => null;
        public int? HosterRegistrationId => null;
        public string? ArchiverName => null;
        public int? LinkCrypterRegistrationId => null;
        public int? ReleaseGroupId => null;
        public string? PostedLocationUrl => null;
        public string? DownloadLink => null;
        public string? ArchiveFileName => null;
        public string? UploadId => null;
        public int? Page => null;
        public int? PageSize => null;
    }
}
