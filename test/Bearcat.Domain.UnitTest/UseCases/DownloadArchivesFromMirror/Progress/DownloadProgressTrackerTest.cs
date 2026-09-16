using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.DownloadArchivesFromMirror.Progress;

public class DownloadProgressTrackerTest
{
    [Test]
    public void Get_NotTracking_ReturnsNull()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();

        // Act
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldBeNull();
    }

    [Test]
    public void Get_AfterStopTracking_ReturnsNull()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(1, "Rapidgator", [Planned(1, "archive.part01.rar", 1000)]);
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 1000);

        // Act
        tracker.StopTracking(1);

        // Assert
        tracker.Get(1).ShouldBeNull();
    }

    [Test]
    public void Get_FreshlyTrackedWithoutPlannedFiles_HasNoFiles()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();

        // Act
        tracker.StartTracking(1, "Rapidgator", []);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.Files.ShouldBeEmpty();
        snapshot.DownloadedBytes.ShouldBe(0);
        snapshot.TotalBytes.ShouldBe(0);
        snapshot.Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_FreshlyTracked_ListsPlannedFilesWithoutProgress()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();

        // Act
        tracker.StartTracking(
            1,
            "Rapidgator",
            [Planned(1, "archive.part01.rar", 600), Planned(2, "archive.part02.rar", 400)]
        );
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.HosterName.ShouldBe("Rapidgator");
        snapshot.Files.Count.ShouldBe(2);
        snapshot.DownloadedBytes.ShouldBe(0);
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(0);
        snapshot.Files[0].DownloadedBytes.ShouldBe(0);
        snapshot.Files[0].TotalBytes.ShouldBe(600);
        snapshot.Files[0].Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_QueuedFileHasNotStarted_PercentageCoversAllPlannedFiles()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(
            1,
            "Rapidgator",
            [
                Planned(1, "archive.part01.rar", 1000),
                Planned(2, "archive.part02.rar", 1000),
                Planned(3, "archive.part03.rar", 1000),
            ]
        );

        // Act
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 1000);
        tracker.AddBytes(1, archiveFileId: 1, bytes: 1000);
        tracker.BeginFile(1, archiveFileId: 2, fileName: "archive.part02.rar", totalBytes: 1000);
        tracker.AddBytes(1, archiveFileId: 2, bytes: 1000);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.Files.Count.ShouldBe(3);
        snapshot.DownloadedBytes.ShouldBe(2000);
        snapshot.TotalBytes.ShouldBe(3000);
        snapshot.Percentage.ShouldBe(67);
        snapshot.Files[2].DownloadedBytes.ShouldBe(0);
        snapshot.Files[2].Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_PlannedFileWithUnknownSize_ReportsIndeterminateTotal()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(
            1,
            "Rapidgator",
            [Planned(1, "archive.part01.rar", 1000), Planned(2, "archive.part02.rar", null)]
        );

        // Act
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 1000);
        tracker.AddBytes(1, archiveFileId: 1, bytes: 1000);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.DownloadedBytes.ShouldBe(1000);
        snapshot.TotalBytes.ShouldBe(0);
        snapshot.Percentage.ShouldBe(0);
        snapshot.Files[1].TotalBytes.ShouldBe(0);
    }

    [Test]
    public void BeginFile_WithoutContentLength_KeepsPlannedSize()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(1, "Rapidgator", [Planned(1, "archive.part01.rar", 1000)]);

        // Act
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: null);
        tracker.AddBytes(1, archiveFileId: 1, bytes: 250);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(25);
    }

    [Test]
    public void BeginFile_WithContentLength_OverridesPlannedSize()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(1, "Rapidgator", [Planned(1, "archive.part01.rar", 1000)]);

        // Act
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 2000);
        tracker.AddBytes(1, archiveFileId: 1, bytes: 500);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TotalBytes.ShouldBe(2000);
        snapshot.Percentage.ShouldBe(25);
    }

    [Test]
    public void Get_AfterAddingBytes_ReportsFileProgress()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(1, "Rapidgator", [Planned(7, "archive.part01.rar", 1000)]);
        tracker.BeginFile(1, archiveFileId: 7, fileName: "archive.part01.rar", totalBytes: 1000);

        // Act
        tracker.AddBytes(1, archiveFileId: 7, bytes: 250);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.ArchiveId.ShouldBe(1);
        snapshot.DownloadedBytes.ShouldBe(250);
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(25);
        snapshot.Files.Count.ShouldBe(1);
        snapshot.Files[0].ArchiveFileId.ShouldBe(7);
        snapshot.Files[0].FileName.ShouldBe("archive.part01.rar");
        snapshot.Files[0].DownloadedBytes.ShouldBe(250);
        snapshot.Files[0].Percentage.ShouldBe(25);
    }

    [Test]
    public void Get_MultipleFiles_SumsBytesAndTotals()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(
            1,
            "Rapidgator",
            [Planned(1, "archive.part01.rar", 600), Planned(2, "archive.part02.rar", 400)]
        );
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 600);
        tracker.BeginFile(1, archiveFileId: 2, fileName: "archive.part02.rar", totalBytes: 400);

        // Act
        tracker.AddBytes(1, archiveFileId: 1, bytes: 300);
        tracker.AddBytes(1, archiveFileId: 2, bytes: 200);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.DownloadedBytes.ShouldBe(500);
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(50);
        snapshot
            .Files.Select(file => file.FileName)
            .ShouldBe(["archive.part01.rar", "archive.part02.rar"]);
    }

    [Test]
    public void Get_TotalBytesUnknownForOneFile_ReportsZeroTotalAndPercentage()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(
            1,
            "Rapidgator",
            [Planned(1, "archive.part01.rar", 600), Planned(2, "archive.part02.rar", null)]
        );
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 600);
        tracker.BeginFile(1, archiveFileId: 2, fileName: "archive.part02.rar", totalBytes: null);

        // Act
        tracker.AddBytes(1, archiveFileId: 1, bytes: 300);
        tracker.AddBytes(1, archiveFileId: 2, bytes: 100);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.DownloadedBytes.ShouldBe(400);
        snapshot.TotalBytes.ShouldBe(0);
        snapshot.Percentage.ShouldBe(0);
        snapshot.Files[1].TotalBytes.ShouldBe(0);
        snapshot.Files[1].DownloadedBytes.ShouldBe(100);
        snapshot.Files[1].Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_DownloadedBytesExceedTotal_ClampsToTotal()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(1, "Rapidgator", [Planned(1, "archive.part01.rar", 1000)]);
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 1000);

        // Act
        tracker.AddBytes(1, archiveFileId: 1, bytes: 1500);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.DownloadedBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(100);
    }

    [Test]
    public void Get_AfterBeginFileOfSameFile_DiscardsThatFilesBytesOnly()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();
        tracker.StartTracking(
            1,
            "Rapidgator",
            [Planned(1, "archive.part01.rar", 600), Planned(2, "archive.part02.rar", 400)]
        );
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 600);
        tracker.BeginFile(1, archiveFileId: 2, fileName: "archive.part02.rar", totalBytes: 400);
        tracker.AddBytes(1, archiveFileId: 1, bytes: 400);
        tracker.AddBytes(1, archiveFileId: 2, bytes: 200);

        // Act
        tracker.BeginFile(1, archiveFileId: 1, fileName: "archive.part01.rar", totalBytes: 600);
        tracker.AddBytes(1, archiveFileId: 1, bytes: 100);
        var snapshot = tracker.Get(1);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.DownloadedBytes.ShouldBe(300);
        snapshot.Files[0].DownloadedBytes.ShouldBe(100);
        snapshot.Files[1].DownloadedBytes.ShouldBe(200);
    }

    [Test]
    public void AddBytes_NotTracking_DoesNotThrow()
    {
        // Arrange
        var tracker = new DownloadProgressTracker();

        // Act
        tracker.AddBytes(42, archiveFileId: 1, bytes: 100);

        // Assert
        tracker.Get(42).ShouldBeNull();
    }

    private static PlannedDownloadFile Planned(int archiveFileId, string fileName, long? sizeBytes)
    {
        return new PlannedDownloadFile(
            ArchiveFileId: archiveFileId,
            FileName: fileName,
            SizeBytes: sizeBytes
        );
    }
}
