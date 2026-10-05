using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.Home.Uploads;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.Home.Uploads;

public class UploadPartListBuilderTest
{
    [Test]
    public void Build_ArchiveFilesWithOnlineStates_MapsOnlineAndOtherStatesToOnlineAndOffline()
    {
        // Arrange
        List<RunningUploadReadModel.ArchiveFileReadModel> archiveFiles =
        [
            new("/archives/release.part1.rar", OnlineState.Online),
            new("/archives/release.part2.rar", OnlineState.Offline),
            new("/archives/release.part3.rar", OnlineState.Unknown),
        ];

        // Act
        var parts = UploadPartListBuilder.Build(archiveFiles, snapshot: null);

        // Assert
        parts
            .Select(part => part.State)
            .ShouldBe([UploadPartState.Online, UploadPartState.Offline, UploadPartState.Offline]);
    }

    [Test]
    public void Build_SnapshotFileHasTransferredBytes_MatchesByFileNameAndReturnsUploading()
    {
        // Arrange
        List<RunningUploadReadModel.ArchiveFileReadModel> archiveFiles =
        [
            new("/archives/release.part1.rar", null),
            new("/archives/release.part2.rar", null),
        ];
        var uploadingFile = CreateFileProgress(1, "release.part1.rar", transferredBytes: 40);
        var waitingFile = CreateFileProgress(2, "release.part2.rar", transferredBytes: 0);
        var snapshot = CreateSnapshot([uploadingFile, waitingFile]);

        // Act
        var parts = UploadPartListBuilder.Build(archiveFiles, snapshot);

        // Assert
        parts.Count.ShouldBe(2);
        parts[0]
            .ShouldBe(
                new UploadPart("release.part1.rar", UploadPartState.Uploading, null, uploadingFile)
            );
        parts[1]
            .ShouldBe(
                new UploadPart("release.part2.rar", UploadPartState.Pending, null, waitingFile)
            );
    }

    [Test]
    public void Build_OnlineStateKnown_OnlineStateWinsOverSnapshotProgress()
    {
        // Arrange
        List<RunningUploadReadModel.ArchiveFileReadModel> archiveFiles =
        [
            new("/archives/release.part1.rar", OnlineState.Online),
        ];
        var snapshot = CreateSnapshot([
            CreateFileProgress(1, "release.part1.rar", transferredBytes: 100),
        ]);

        // Act
        var parts = UploadPartListBuilder.Build(archiveFiles, snapshot);

        // Assert
        parts.Single().State.ShouldBe(UploadPartState.Online);
    }

    [Test]
    public void Build_NoSnapshotAndNoOnlineState_ReturnsPendingWithoutProgress()
    {
        // Arrange
        List<RunningUploadReadModel.ArchiveFileReadModel> archiveFiles =
        [
            new("/archives/release.part1.rar", null),
        ];

        // Act
        var parts = UploadPartListBuilder.Build(archiveFiles, snapshot: null);

        // Assert
        parts
            .Single()
            .ShouldBe(new UploadPart("release.part1.rar", UploadPartState.Pending, null, null));
    }

    private static TransferFileProgressSnapshot CreateFileProgress(
        int fileId,
        string fileName,
        long transferredBytes
    )
    {
        return new TransferFileProgressSnapshot(
            FileId: fileId,
            FileName: fileName,
            SourceName: "Hoster",
            ProxyServerName: null,
            TransferredBytes: transferredBytes,
            TotalBytes: 100
        );
    }

    private static TransferProgressSnapshot CreateSnapshot(
        IReadOnlyList<TransferFileProgressSnapshot> files
    )
    {
        return new TransferProgressSnapshot(
            Identifier: new TransferIdentifier(TransferType.Upload, 1),
            BytesPerSecond: 0,
            TransferredBytes: files.Sum(file => file.TransferredBytes),
            TotalBytes: files.Sum(file => file.TotalBytes),
            Files: files
        );
    }
}
