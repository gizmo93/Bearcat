using Bearcat.Hosters.GoFile.Api;
using Bearcat.Hosters.GoFile.Api.GetFileInfo;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using GoFileApiClient = Bearcat.Hosters.GoFile.Api.ApiClient;

namespace Bearcat.Hosters.UnitTest.GoFile;

public class ApiClientTest
{
    private Mock<IGoFileApi> apiMock = null!;
    private GoFileApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IGoFileApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<GoFileApiClient>>();

        apiClient = new GoFileApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        )
        {
            RateLimitRetryDelay = TimeSpan.Zero,
            FileCheckTimeout = TimeSpan.FromSeconds(5),
        };
    }

    [Test]
    public async Task CreateUploadFolderIdAsync_ExistingRootFolderWithName_ReturnsExistingFolderId()
    {
        // Arrange
        SetupAccountRootFolder();
        apiMock
            .Setup(x =>
                x.GetContentAsync(
                    "root-folder-id",
                    "Bearer api-key",
                    "release-folder",
                    "createTime",
                    1,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Bearcat.Hosters.GoFile.Api.GetContent.Response
                {
                    Status = "ok",
                    Data = new Bearcat.Hosters.GoFile.Api.GetContent.Data
                    {
                        Children = new Dictionary<string, Bearcat.Hosters.GoFile.Api.GetContent.Content>
                        {
                            ["file-id"] = new()
                            {
                                Id = "file-id",
                                Name = "release-folder",
                                Type = "file",
                            },
                            ["folder-id"] = new()
                            {
                                Id = "folder-id",
                                Name = "release-folder",
                                Type = "folder",
                            },
                        },
                    },
                }
            );

        // Act
        var result = await apiClient.CreateUploadFolderIdAsync(
            "api-key",
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("folder-id");
        apiMock.Verify(
            x =>
                x.CreateFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<Bearcat.Hosters.GoFile.Api.CreateFolder.Request>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateUploadFolderIdAsync_NoExistingRootFolderWithName_CreatesFolder()
    {
        // Arrange
        SetupAccountRootFolder();
        apiMock
            .Setup(x =>
                x.GetContentAsync(
                    "root-folder-id",
                    "Bearer api-key",
                    "release-folder",
                    "createTime",
                    1,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Bearcat.Hosters.GoFile.Api.GetContent.Response
                {
                    Status = "ok",
                    Data = new Bearcat.Hosters.GoFile.Api.GetContent.Data
                    {
                        Children = new Dictionary<string, Bearcat.Hosters.GoFile.Api.GetContent.Content>
                        {
                            ["other-folder-id"] = new()
                            {
                                Id = "other-folder-id",
                                Name = "other-folder",
                                Type = "folder",
                            },
                        },
                    },
                }
            );
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    "Bearer api-key",
                    It.Is<Bearcat.Hosters.GoFile.Api.CreateFolder.Request>(request =>
                        request.ParentFolderId == "root-folder-id"
                        && request.FolderName == "release-folder"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Bearcat.Hosters.GoFile.Api.CreateFolder.Response
                {
                    Status = "ok",
                    Data = new Bearcat.Hosters.GoFile.Api.CreateFolder.Data
                    {
                        Id = "created-folder-id",
                    },
                }
            );

        // Act
        var result = await apiClient.CreateUploadFolderIdAsync(
            "api-key",
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("created-folder-id");
    }

    [Test]
    public async Task CheckOnlineStatusAsync_ManyLinks_ChecksUpToFiveLinksInParallel()
    {
        // Arrange
        var fileUrls = Enumerable
            .Range(1, 12)
            .Select(index => $"https://gofile.io/d/file-{index}")
            .ToList();
        var expectedParallelRequests = 5;
        var currentParallelRequests = 0;
        var maximumParallelRequests = 0;
        var firstBatchStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var allowRequestsToFinish = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var onlineResponse = new Response
        {
            Status = "ok",
            Data = new Data { Id = "file-id", Type = "file" },
        };

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(
                    It.IsAny<string>(),
                    "Bearer api-key",
                    "4fd6sg89d7s6",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                var current = Interlocked.Increment(ref currentParallelRequests);
                UpdateMaximum(ref maximumParallelRequests, current);

                if (current == expectedParallelRequests)
                {
                    firstBatchStarted.TrySetResult();
                }

                return allowRequestsToFinish.Task.ContinueWith(_ =>
                {
                    Interlocked.Decrement(ref currentParallelRequests);
                    return onlineResponse;
                });
            });

        // Act
        var checkTask = apiClient.CheckOnlineStatusAsync(
            fileUrls,
            "api-key",
            CancellationToken.None
        );

        await firstBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        allowRequestsToFinish.TrySetResult();
        var result = await checkTask;

        // Assert
        result.Count.ShouldBe(12);
        result.Values.ShouldAllBe(status => status.IsOnline);
        result.Values.ShouldAllBe(status => status.ErrorMessage == null);
        maximumParallelRequests.ShouldBe(expectedParallelRequests);
    }

    [Test]
    public async Task CheckOnlineStatusAsync_FolderResponse_ReturnsOffline()
    {
        // Arrange
        var fileUrl = "https://gofile.io/d/folder-id";

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(
                    "folder-id",
                    "Bearer api-key",
                    "4fd6sg89d7s6",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Response
                {
                    Status = "ok",
                    Data = new Data { Id = "folder-id", Type = "folder" },
                }
            );

        // Act
        var result = await apiClient.CheckOnlineStatusAsync(
            [fileUrl],
            "api-key",
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeFalse();
        result[fileUrl].ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task CheckOnlineStatusAsync_EmptyUrl_ReturnsOfflineWithoutApiCall()
    {
        // Arrange
        var fileUrl = string.Empty;

        // Act
        var result = await apiClient.CheckOnlineStatusAsync(
            [fileUrl],
            "api-key",
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeFalse();
        result[fileUrl].ErrorMessage.ShouldBe("Invalid GoFile URL");
        apiMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CheckOnlineStatusAsync_FileInfoRequestTimesOut_ReturnsError()
    {
        // Arrange
        var fileUrl = "https://gofile.io/d/file-id";
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<GoFileApiClient>>();
        var timeoutApiClient = new GoFileApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        )
        {
            RateLimitRetryDelay = TimeSpan.Zero,
            FileCheckTimeout = TimeSpan.FromMilliseconds(10),
        };

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(
                    "file-id",
                    "Bearer api-key",
                    "4fd6sg89d7s6",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string, string, CancellationToken>(
                async (_, _, _, cancellationToken) =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    return new Response();
                }
            );

        // Act
        var result = await timeoutApiClient.CheckOnlineStatusAsync(
            [fileUrl],
            "api-key",
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeFalse();
        result[fileUrl].ErrorMessage.ShouldBe("GoFile file check timed out after 10 milliseconds");
    }

    private void SetupAccountRootFolder()
    {
        apiMock
            .Setup(x => x.GetAccountAsync("Bearer api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Bearcat.Hosters.GoFile.Api.GetAccountId.Response
                {
                    Status = "ok",
                    Data = new Bearcat.Hosters.GoFile.Api.GetAccountId.Data
                    {
                        Id = "account-id",
                    },
                }
            );
        apiMock
            .Setup(x =>
                x.GetAccountInfosAsync(
                    "account-id",
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Bearcat.Hosters.GoFile.Api.GetAccountInfos.Response
                {
                    Status = "ok",
                    Data = new Bearcat.Hosters.GoFile.Api.GetAccountInfos.Data
                    {
                        Id = "account-id",
                        RootFolder = "root-folder-id",
                    },
                }
            );
    }

    private static void UpdateMaximum(ref int maximum, int current)
    {
        var initialMaximum = maximum;

        while (
            current > initialMaximum
            && Interlocked.CompareExchange(ref maximum, current, initialMaximum) != initialMaximum
        )
        {
            initialMaximum = maximum;
        }
    }
}
