using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleases.UploadHistory;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases.UploadHistory;

public class UploadHistoryMarkerKindResolverTest
{
    [TestCase(UploadState.WaitingForArchive)]
    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public void GetMarkerKind_UploadIsRunning_ReturnsInProgressRegardlessOfOnlineState(
        UploadState uploadState
    )
    {
        // Act
        var markerKind = UploadHistoryMarkerKindResolver.GetMarkerKind(
            uploadState,
            OnlineState.Online
        );

        // Assert
        markerKind.ShouldBe(UploadHistoryMarkerKind.InProgress);
    }

    [TestCase(OnlineState.Online, UploadHistoryMarkerKind.Online)]
    [TestCase(OnlineState.PartiallyOnline, UploadHistoryMarkerKind.PartiallyOnline)]
    [TestCase(OnlineState.Offline, UploadHistoryMarkerKind.Offline)]
    [TestCase(OnlineState.Unknown, UploadHistoryMarkerKind.Unknown)]
    public void GetMarkerKind_UploadIsCompleted_ReturnsMarkerKindOfOnlineState(
        OnlineState onlineState,
        UploadHistoryMarkerKind expectedMarkerKind
    )
    {
        // Act
        var markerKind = UploadHistoryMarkerKindResolver.GetMarkerKind(
            UploadState.Completed,
            onlineState
        );

        // Assert
        markerKind.ShouldBe(expectedMarkerKind);
    }

    [TestCase(UploadState.Failed)]
    [TestCase(UploadState.Canceled)]
    public void GetMarkerKind_UploadStoppedWithoutOnlineState_ReturnsUnknown(
        UploadState uploadState
    )
    {
        // Act
        var markerKind = UploadHistoryMarkerKindResolver.GetMarkerKind(
            uploadState,
            OnlineState.Unknown
        );

        // Assert
        markerKind.ShouldBe(UploadHistoryMarkerKind.Unknown);
    }
}
