using Bearcat.Domain.Shared.Transfers;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.Transfers;

public class TransferProgressTrackerTest
{
    private static readonly TransferIdentifier Identifier = new(TransferKind.MirrorDownload, 1);

    [Test]
    public void Get_NotTracking_ReturnsNull()
    {
        // Arrange
        var tracker = new TransferProgressTracker();

        // Act
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldBeNull();
    }

    [Test]
    public void Get_AfterStopTracking_ReturnsNull()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(Identifier, [Planned(1, "archive.part01.rar", 1000)]);
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 1000);

        // Act
        tracker.StopTracking(Identifier);

        // Assert
        tracker.Get(Identifier).ShouldBeNull();
    }

    [Test]
    public void Get_SameIdWithDifferentKind_IsTrackedSeparately()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        var uploadKey = new TransferIdentifier(TransferKind.Upload, Identifier.Id);
        tracker.StartTracking(Identifier, [Planned(1, "archive.part01.rar", 1000)]);

        // Act
        var snapshot = tracker.Get(uploadKey);

        // Assert
        snapshot.ShouldBeNull();
    }

    [Test]
    public void Get_FreshlyTrackedWithoutPlannedFiles_HasNoFiles()
    {
        // Arrange
        var tracker = new TransferProgressTracker();

        // Act
        tracker.StartTracking(Identifier, []);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.Files.ShouldBeEmpty();
        snapshot.TransferredBytes.ShouldBe(0);
        snapshot.TotalBytes.ShouldBe(0);
        snapshot.Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_FreshlyTracked_ListsPlannedFilesWithoutProgress()
    {
        // Arrange
        var tracker = new TransferProgressTracker();

        // Act
        tracker.StartTracking(
            Identifier,
            [Planned(1, "archive.part01.rar", 600), Planned(2, "archive.part02.rar", 400)]
        );
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.Identifier.ShouldBe(Identifier);
        snapshot.SourceName.ShouldBe("Rapidgator");
        snapshot.Files.Count.ShouldBe(2);
        snapshot.TransferredBytes.ShouldBe(0);
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(0);
        snapshot.Files[0].TransferredBytes.ShouldBe(0);
        snapshot.Files[0].TotalBytes.ShouldBe(600);
        snapshot.Files[0].Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_PlannedFilesAlreadyTransferred_CountTowardsTransferredBytes()
    {
        // Arrange
        var tracker = new TransferProgressTracker();

        // Act
        tracker.StartTracking(
            Identifier,
            [
                Planned(1, "archive.part01.rar", 250, isAlreadyTransferred: true),
                Planned(2, "archive.part02.rar", 750),
            ]
        );
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TransferredBytes.ShouldBe(250);
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(25);
        snapshot.Files[0].Percentage.ShouldBe(100);
    }

    [Test]
    public void Get_AfterAddingBytes_AddsToAlreadyTransferredFiles()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(
            Identifier,
            [
                Planned(1, "archive.part01.rar", 250, isAlreadyTransferred: true),
                Planned(2, "archive.part02.rar", 750),
            ]
        );

        // Act
        tracker.AddBytes(Identifier, fileId: 2, bytes: 250);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TransferredBytes.ShouldBe(500);
        snapshot.Percentage.ShouldBe(50);
    }

    [Test]
    public void Get_QueuedFileHasNotStarted_PercentageCoversAllPlannedFiles()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(
            Identifier,
            [
                Planned(1, "archive.part01.rar", 1000),
                Planned(2, "archive.part02.rar", 1000),
                Planned(3, "archive.part03.rar", 1000),
            ]
        );

        // Act
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 1000);
        tracker.AddBytes(Identifier, fileId: 1, bytes: 1000);
        tracker.BeginFile(Identifier, 2, "archive.part02.rar", "Rapidgator", totalBytes: 1000);
        tracker.AddBytes(Identifier, fileId: 2, bytes: 1000);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.Files.Count.ShouldBe(3);
        snapshot.TransferredBytes.ShouldBe(2000);
        snapshot.TotalBytes.ShouldBe(3000);
        snapshot.Percentage.ShouldBe(67);
        snapshot.Files[2].TransferredBytes.ShouldBe(0);
        snapshot.Files[2].Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_PlannedFileWithUnknownSize_ReportsIndeterminateTotal()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(
            Identifier,
            [Planned(1, "archive.part01.rar", 1000), Planned(2, "archive.part02.rar", null)]
        );

        // Act
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 1000);
        tracker.AddBytes(Identifier, fileId: 1, bytes: 1000);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TransferredBytes.ShouldBe(1000);
        snapshot.TotalBytes.ShouldBe(0);
        snapshot.Percentage.ShouldBe(0);
        snapshot.Files[1].TotalBytes.ShouldBe(0);
    }

    [Test]
    public void Get_PlannedEmptyFile_KeepsTotalKnown()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(
            Identifier,
            [Planned(1, "archive.part01.rar", 1000), Planned(2, "archive.sfv", 0)]
        );

        // Act
        tracker.AddBytes(Identifier, fileId: 1, bytes: 500);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(50);
    }

    [Test]
    public void BeginFile_WithoutTotalBytes_KeepsPlannedSize()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(Identifier, [Planned(1, "archive.part01.rar", 1000)]);

        // Act
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: null);
        tracker.AddBytes(Identifier, fileId: 1, bytes: 250);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(25);
    }

    [Test]
    public void BeginFile_WithTotalBytes_OverridesPlannedSize()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(Identifier, [Planned(1, "archive.part01.rar", 1000)]);

        // Act
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 2000);
        tracker.AddBytes(Identifier, fileId: 1, bytes: 500);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TotalBytes.ShouldBe(2000);
        snapshot.Percentage.ShouldBe(25);
    }

    [Test]
    public void BeginFile_WithOtherSource_ReportsNewSourceName()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(Identifier, [Planned(1, "archive.part01.rar", 1000)]);

        // Act
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Keep2Share", totalBytes: 1000);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.SourceName.ShouldBe("Keep2Share");
        snapshot.Files[0].SourceName.ShouldBe("Keep2Share");
    }

    [Test]
    public void Get_AfterAddingBytes_ReportsFileProgress()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(Identifier, [Planned(7, "archive.part01.rar", 1000)]);
        tracker.BeginFile(Identifier, 7, "archive.part01.rar", "Rapidgator", totalBytes: 1000);

        // Act
        tracker.AddBytes(Identifier, fileId: 7, bytes: 250);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TransferredBytes.ShouldBe(250);
        snapshot.TotalBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(25);
        snapshot.Files.Count.ShouldBe(1);
        snapshot.Files[0].FileId.ShouldBe(7);
        snapshot.Files[0].FileName.ShouldBe("archive.part01.rar");
        snapshot.Files[0].TransferredBytes.ShouldBe(250);
        snapshot.Files[0].Percentage.ShouldBe(25);
    }

    [Test]
    public void Get_MultipleFiles_SumsBytesAndTotals()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(
            Identifier,
            [Planned(1, "archive.part01.rar", 600), Planned(2, "archive.part02.rar", 400)]
        );
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 600);
        tracker.BeginFile(Identifier, 2, "archive.part02.rar", "Rapidgator", totalBytes: 400);

        // Act
        tracker.AddBytes(Identifier, fileId: 1, bytes: 300);
        tracker.AddBytes(Identifier, fileId: 2, bytes: 200);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TransferredBytes.ShouldBe(500);
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
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(
            Identifier,
            [Planned(1, "archive.part01.rar", 600), Planned(2, "archive.part02.rar", null)]
        );
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 600);
        tracker.BeginFile(Identifier, 2, "archive.part02.rar", "Rapidgator", totalBytes: null);

        // Act
        tracker.AddBytes(Identifier, fileId: 1, bytes: 300);
        tracker.AddBytes(Identifier, fileId: 2, bytes: 100);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TransferredBytes.ShouldBe(400);
        snapshot.TotalBytes.ShouldBe(0);
        snapshot.Percentage.ShouldBe(0);
        snapshot.Files[1].TotalBytes.ShouldBe(0);
        snapshot.Files[1].TransferredBytes.ShouldBe(100);
        snapshot.Files[1].Percentage.ShouldBe(0);
    }

    [Test]
    public void Get_TransferredBytesExceedTotal_ClampsToTotal()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(Identifier, [Planned(1, "archive.part01.rar", 1000)]);
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 1000);

        // Act
        tracker.AddBytes(Identifier, fileId: 1, bytes: 1500);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TransferredBytes.ShouldBe(1000);
        snapshot.Percentage.ShouldBe(100);
    }

    [Test]
    public void Get_AfterBeginFileOfSameFile_DiscardsThatFilesBytesOnly()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        tracker.StartTracking(
            Identifier,
            [Planned(1, "archive.part01.rar", 600), Planned(2, "archive.part02.rar", 400)]
        );
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 600);
        tracker.BeginFile(Identifier, 2, "archive.part02.rar", "Rapidgator", totalBytes: 400);
        tracker.AddBytes(Identifier, fileId: 1, bytes: 400);
        tracker.AddBytes(Identifier, fileId: 2, bytes: 200);

        // Act
        tracker.BeginFile(Identifier, 1, "archive.part01.rar", "Rapidgator", totalBytes: 600);
        tracker.AddBytes(Identifier, fileId: 1, bytes: 100);
        var snapshot = tracker.Get(Identifier);

        // Assert
        snapshot.ShouldNotBeNull();
        snapshot.TransferredBytes.ShouldBe(300);
        snapshot.Files[0].TransferredBytes.ShouldBe(100);
        snapshot.Files[1].TransferredBytes.ShouldBe(200);
    }

    [Test]
    public void AddBytes_NotTracking_DoesNotThrow()
    {
        // Arrange
        var tracker = new TransferProgressTracker();
        var untrackedKey = new TransferIdentifier(TransferKind.Upload, 42);

        // Act
        tracker.AddBytes(untrackedKey, fileId: 1, bytes: 100);

        // Assert
        tracker.Get(untrackedKey).ShouldBeNull();
    }

    private static TransferFile Planned(
        int fileId,
        string fileName,
        long? sizeBytes,
        bool isAlreadyTransferred = false
    )
    {
        return new TransferFile(
            FileId: fileId,
            FileName: fileName,
            SourceName: "Rapidgator",
            SizeBytes: sizeBytes,
            IsAlreadyTransferred: isAlreadyTransferred
        );
    }
}
