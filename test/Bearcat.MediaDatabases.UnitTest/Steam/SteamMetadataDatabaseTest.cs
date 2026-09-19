using System.Net;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Steam;
using Bearcat.MediaDatabases.Steam.Api;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.MediaDatabases.UnitTest.Steam;

public class SteamMetadataDatabaseTest
{
    [Test]
    public async Task GetByExternalIdAsync_GameFound_MapsMetadata()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.GetAppDetailsAsync("1172710", "german", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse(CreateAppDetails("1172710", CreateGameData())));
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        var metadata = await database.GetByExternalIdAsync(
            new SteamConfig(),
            CreateLookup(steamAppId: "1172710", title: null, languageCode: "de")
        );

        // Assert
        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("Bearcat Odyssey");
        metadata.Description.ShouldBe("An odyssey through the archives.");
        metadata.Genre.ShouldBe("Action, Adventure");
        metadata.CoverUrl.ShouldBe("https://cdn.test/header.jpg");
        metadata.DatabaseUrl.ShouldBe("https://store.steampowered.com/app/1172710");
    }

    [Test]
    public async Task GetByExternalIdAsync_UnsuccessfulAppDetails_ReturnsNull()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.GetAppDetailsAsync("1172710", "english", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new Dictionary<string, SteamAppDetailsResponse>
                    {
                        ["1172710"] = new(Success: false, Data: null),
                    }
                )
            );
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        var metadata = await database.GetByExternalIdAsync(
            new SteamConfig(),
            CreateLookup(steamAppId: "1172710", title: null, languageCode: null)
        );

        // Assert
        metadata.ShouldBeNull();
    }

    [Test]
    public async Task GetByExternalIdAsync_NonGameType_ReturnsNull()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.GetAppDetailsAsync("1172710", "english", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    CreateAppDetails("1172710", CreateGameData() with { Type = "dlc" })
                )
            );
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        var metadata = await database.GetByExternalIdAsync(
            new SteamConfig(),
            CreateLookup(steamAppId: "1172710", title: null, languageCode: null)
        );

        // Assert
        metadata.ShouldBeNull();
    }

    [Test]
    public async Task GetByExternalIdAsync_NoSteamAppId_ReturnsNull()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        var metadata = await database.GetByExternalIdAsync(
            new SteamConfig(),
            CreateLookup(steamAppId: null, title: "Bearcat Odyssey", languageCode: null)
        );

        // Assert
        metadata.ShouldBeNull();
    }

    [Test]
    public async Task GetByTitleAsync_SearchHit_MapsFirstResult()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.SearchStoreAsync(
                    "Bearcat Odyssey",
                    "english",
                    "US",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new SteamStoreSearchResponse(
                        2,
                        [
                            new SteamStoreSearchItemResponse(1172710, "app", "Bearcat Odyssey"),
                            new SteamStoreSearchItemResponse(999, "app", "Other"),
                        ]
                    )
                )
            );
        api.Setup(item =>
                item.GetAppDetailsAsync("1172710", "english", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse(CreateAppDetails("1172710", CreateGameData())));
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        var metadata = await database.GetByTitleAsync(
            new SteamConfig(),
            CreateLookup(steamAppId: null, title: "Bearcat Odyssey", languageCode: null)
        );

        // Assert
        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("Bearcat Odyssey");
        metadata.DatabaseUrl.ShouldBe("https://store.steampowered.com/app/1172710");
    }

    [Test]
    public async Task GetByTitleAsync_NoSearchResults_ReturnsNull()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.SearchStoreAsync(
                    "Bearcat Odyssey",
                    "english",
                    "US",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateApiResponse(new SteamStoreSearchResponse(0, [])));
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        var metadata = await database.GetByTitleAsync(
            new SteamConfig(),
            CreateLookup(steamAppId: null, title: "Bearcat Odyssey", languageCode: null)
        );

        // Assert
        metadata.ShouldBeNull();
    }

    [Test]
    public async Task GetByExternalIdAsync_RateLimited_Throws()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.GetAppDetailsAsync("1172710", "english", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateErrorResponse<Dictionary<string, SteamAppDetailsResponse>>(
                    HttpStatusCode.TooManyRequests
                )
            );
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        var exception = await Should.ThrowAsync<MediaMetadataDatabaseRateLimitExceededException>(
            async () =>
                await database.GetByExternalIdAsync(
                    new SteamConfig(),
                    CreateLookup(steamAppId: "1172710", title: null, languageCode: null)
                )
        );

        // Assert
        exception.ResetAt.ShouldBeNull();
    }

    [Test]
    public async Task GetByExternalIdAsync_GermanLanguageCode_RequestsGermanLanguage()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.GetAppDetailsAsync("1172710", "german", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse(CreateAppDetails("1172710", CreateGameData())));
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        await database.GetByExternalIdAsync(
            new SteamConfig(),
            CreateLookup(steamAppId: "1172710", title: null, languageCode: "de")
        );

        // Assert
        api.Verify(
            item => item.GetAppDetailsAsync("1172710", "german", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task GetByExternalIdAsync_NoLanguageCode_RequestsEnglishLanguage()
    {
        // Arrange
        var api = new Mock<ISteamApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.GetAppDetailsAsync("1172710", "english", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse(CreateAppDetails("1172710", CreateGameData())));
        var database = new SteamMetadataDatabase(api.Object);

        // Act
        await database.GetByExternalIdAsync(
            new SteamConfig(),
            CreateLookup(steamAppId: "1172710", title: null, languageCode: null)
        );

        // Assert
        api.Verify(
            item => item.GetAppDetailsAsync("1172710", "english", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private static MediaMetadataLookup CreateLookup(
        string? steamAppId,
        string? title,
        string? languageCode
    )
    {
        return new MediaMetadataLookup(
            MediaKind.Game,
            null,
            steamAppId,
            title,
            null,
            null,
            null,
            languageCode
        );
    }

    private static SteamAppDataResponse CreateGameData()
    {
        return new SteamAppDataResponse(
            Type: "game",
            Name: "Bearcat Odyssey",
            ShortDescription: "An odyssey through the archives.",
            HeaderImage: "https://cdn.test/header.jpg",
            Developers: ["Bearcat Studios"],
            Publishers: ["Bearcat Publishing"],
            Genres:
            [
                new SteamGenreResponse("1", "Action"),
                new SteamGenreResponse("25", "Adventure"),
            ],
            ReleaseDate: new SteamReleaseDateResponse(false, "1 Jan, 2026")
        );
    }

    private static Dictionary<string, SteamAppDetailsResponse> CreateAppDetails(
        string appId,
        SteamAppDataResponse data
    )
    {
        return new Dictionary<string, SteamAppDetailsResponse>
        {
            [appId] = new(Success: true, Data: data),
        };
    }

    private static ApiResponse<T> CreateApiResponse<T>(T content)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(),
            },
            content,
            new RefitSettings(),
            error: null
        );
    }

    private static ApiResponse<T> CreateErrorResponse<T>(HttpStatusCode statusCode)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            default,
            new RefitSettings(),
            error: null
        );
    }
}
