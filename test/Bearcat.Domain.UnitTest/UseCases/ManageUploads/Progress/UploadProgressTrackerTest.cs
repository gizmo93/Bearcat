using Bearcat.Domain.UseCases.ManageUploads.Progress;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageUploads.Progress;

public class UploadProgressTrackerTest
{
    [Test]
    public void Get_NotTracking_ReturnsNull()
    {
        // Arrange
        var tracker = new UploadProgressTracker();

        // Act
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldBeNull();
    }

    [Test]
    public void Get_AfterStopTracking_ReturnsNull()
    {
        // Arrange
        var tracker = new UploadProgressTracker();
        tracker.StartTracking(1, totalBytes: 1000, alreadyUploadedBytes: 0);

        // Act
        tracker.StopTracking(1);

        // Assert
        tracker.Get(1).ShouldBeNull();
    }

    [Test]
    public void Get_FreshlyTracked_ReportsBaselineAsUploadedBytes()
    {
        // Arrange
        var tracker = new UploadProgressTracker();
        tracker.StartTracking(1, totalBytes: 1000, alreadyUploadedBytes: 250);

        // Act
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.UploadedBytes.ShouldBe(250);
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(25);
    }

    [Test]
    public void Get_AfterAddingBytes_AddsToBaseline()
    {
        // Arrange
        var tracker = new UploadProgressTracker();
        tracker.StartTracking(1, totalBytes: 1000, alreadyUploadedBytes: 250);

        // Act
        tracker.AddBytes(1, fileId: 1, 250);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.UploadedBytes.ShouldBe(500);
        snapshot.Percentage.ShouldBe(50);
    }

    [Test]
    public void Get_UploadedBytesExceedTotal_ClampsToTotal()
    {
        // Arrange
        var tracker = new UploadProgressTracker();
        tracker.StartTracking(1, totalBytes: 1000, alreadyUploadedBytes: 900);

        // Act
        tracker.AddBytes(1, fileId: 1, 500);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.UploadedBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(100);
    }

    [Test]
    public void Get_TotalBytesUnknown_ReportsZeroPercentage()
    {
        // Arrange
        var tracker = new UploadProgressTracker();
        tracker.StartTracking(1, totalBytes: 0, alreadyUploadedBytes: 0);

        // Act
        tracker.AddBytes(1, fileId: 1, 100);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TotalBytes.ShouldBe(0);
        snapshot.Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_MultipleFiles_SumsBytesPerFile()
    {
        // Arrange
        var tracker = new UploadProgressTracker();
        tracker.StartTracking(1, totalBytes: 1000, alreadyUploadedBytes: 0);

        // Act
        tracker.AddBytes(1, fileId: 1, 300);
        tracker.AddBytes(1, fileId: 2, 200);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.UploadedBytes.ShouldBe(500);
        snapshot.Percentage.ShouldBe(50);
    }

    [Test]
    public void Get_AfterResetFile_DiscardsThatFilesBytesOnly()
    {
        // Arrange
        var tracker = new UploadProgressTracker();
        tracker.StartTracking(1, totalBytes: 1000, alreadyUploadedBytes: 0);
        tracker.AddBytes(1, fileId: 1, 400);
        tracker.AddBytes(1, fileId: 2, 200);

        // Act
        tracker.ResetFile(1, fileId: 1);
        tracker.AddBytes(1, fileId: 1, 400);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.UploadedBytes.ShouldBe(600);
        snapshot.Percentage.ShouldBe(60);
    }
}
