using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;

public class RemoteSourceDownloadStateServiceTest
{
    private static readonly DateTime CompletedAt = new(
        2026,
        9,
        27,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    [Test]
    public async Task RetryWithoutDownloadingAgainAsync_ArchivesNotExtracted_QueuesVerificationAgain()
    {
        // Arrange
        var download = CreateDownload(RemoteSourceDownloadState.Failed, CompletedAt, null);
        var service = CreateService(download);

        // Act
        await service.RetryWithoutDownloadingAgainAsync(download.Id);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Downloaded);
        download.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task RetryWithoutDownloadingAgainAsync_ArchivesExtracted_QueuesReleaseCreationDirectly()
    {
        // Arrange
        var download = CreateDownload(RemoteSourceDownloadState.Failed, CompletedAt, CompletedAt);
        var service = CreateService(download);

        // Act
        await service.RetryWithoutDownloadingAgainAsync(download.Id);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.ReadyForReleaseCreation);
        download.ErrorMessage.ShouldBeNull();
        download.ArchivesExtractedAt.ShouldBe(CompletedAt);
    }

    [Test]
    public async Task RetryWithoutDownloadingAgainAsync_FailedBeforeDownloadCompleted_Throws()
    {
        // Arrange
        var download = CreateDownload(RemoteSourceDownloadState.Failed, null, null);
        var service = CreateService(download);

        // Act
        var retry = () => service.RetryWithoutDownloadingAgainAsync(download.Id);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(retry);
        download.State.ShouldBe(RemoteSourceDownloadState.Failed);
    }

    [Test]
    public async Task RestartDownloadAsync_Duplicate_QueuesDownload()
    {
        // Arrange
        var download = CreateDownload(RemoteSourceDownloadState.Duplicate, null, null);
        var service = CreateService(download);

        // Act
        await service.RestartDownloadAsync(download.Id);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Pending);
        download.StartedAt.ShouldBeNull();
        download.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task IgnoreDownloadAsync_Duplicate_IgnoresDownload()
    {
        // Arrange
        var download = CreateDownload(RemoteSourceDownloadState.Duplicate, null, null);
        var service = CreateService(download);

        // Act
        await service.IgnoreDownloadAsync(download.Id);

        // Assert
        download.State.ShouldBe(RemoteSourceDownloadState.Ignored);
    }

    [Test]
    public async Task CancelDownloadAsync_Duplicate_Throws()
    {
        // Arrange
        var download = CreateDownload(RemoteSourceDownloadState.Duplicate, null, null);
        var service = CreateService(download);

        // Act
        var cancel = () => service.CancelDownloadAsync(download.Id);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(cancel);
        download.State.ShouldBe(RemoteSourceDownloadState.Duplicate);
    }

    [TestCase(RemoteSourceDownloadState.Verifying)]
    [TestCase(RemoteSourceDownloadState.Extracting)]
    [TestCase(RemoteSourceDownloadState.ReadyForReleaseCreation)]
    public async Task StateChanges_DownloadBeingVerifiedOrExtractedOrReady_AreRejected(
        RemoteSourceDownloadState state
    )
    {
        // Arrange
        var download = CreateDownload(state, CompletedAt, null);
        var service = CreateService(download);

        // Act
        var cancel = () => service.CancelDownloadAsync(download.Id);
        var restart = () => service.RestartDownloadAsync(download.Id);
        var ignore = () => service.IgnoreDownloadAsync(download.Id);
        var retry = () => service.RetryWithoutDownloadingAgainAsync(download.Id);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(cancel);
        await Should.ThrowAsync<InvalidOperationException>(restart);
        await Should.ThrowAsync<InvalidOperationException>(ignore);
        await Should.ThrowAsync<InvalidOperationException>(retry);
        download.State.ShouldBe(state);
    }

    private static RemoteSourceDownloadStateService CreateService(RemoteSourceDownload download)
    {
        var repository = new Mock<IRemoteSourceDownloadRepository>();
        repository
            .Setup(r => r.GetByIdAsync(download.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(download);

        return new RemoteSourceDownloadStateService(
            repository.Object,
            new TransferCancellationRegistry(),
            new RemoteDownloadFolderService(
                Mock.Of<IFileSystemService>(),
                NullLogger<RemoteDownloadFolderService>.Instance
            )
        );
    }

    private static RemoteSourceDownload CreateDownload(
        RemoteSourceDownloadState state,
        DateTime? completedAt,
        DateTime? archivesExtractedAt
    )
    {
        return new RemoteSourceDownload
        {
            Id = 1,
            SourceName = "Main FTP",
            RemoteFolderPath = "/incoming/Release-GRP",
            FolderName = "Release-GRP",
            LocalFolderPath = "/downloads/Release-GRP",
            State = state,
            StartedAt = completedAt,
            CompletedAt = completedAt,
            ArchivesExtractedAt = archivesExtractedAt,
            ErrorMessage = state is RemoteSourceDownloadState.Failed ? "Failed" : null,
        };
    }
}
