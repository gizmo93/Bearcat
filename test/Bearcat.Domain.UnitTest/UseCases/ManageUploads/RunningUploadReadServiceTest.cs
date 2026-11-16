using System.Linq.Expressions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageUploads;

public class RunningUploadReadServiceTest
{
    private static readonly DateTime Midnight = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetRunningUploadsInStartOrderAsync_OldestFirst_ReturnsNewestPrioritizationFirstThenUploadId()
    {
        // Arrange
        var service = CreateService(
            UploadStartOrder.OldestFirst,
            CreateUpload(uploadId: 4, isReupload: false),
            CreateUpload(uploadId: 3, isReupload: true),
            CreateUpload(uploadId: 5, isReupload: false, prioritizedAt: Midnight),
            CreateUpload(uploadId: 2, isReupload: false),
            CreateUpload(uploadId: 6, isReupload: true, prioritizedAt: Midnight.AddMinutes(1))
        );

        // Act
        var result = await service.GetRunningUploadsInStartOrderAsync(CancellationToken.None);

        // Assert
        result.Select(upload => upload.UploadId).ShouldBe([6, 5, 2, 3, 4]);
    }

    [Test]
    public async Task GetRunningUploadsInStartOrderAsync_InitialUploadsBeforeReuploads_ReturnsPrioritizedFirstThenInitialUploadsThenReuploads()
    {
        // Arrange
        var service = CreateService(
            UploadStartOrder.InitialUploadsBeforeReuploads,
            CreateUpload(uploadId: 4, isReupload: false),
            CreateUpload(uploadId: 3, isReupload: true),
            CreateUpload(uploadId: 5, isReupload: true, prioritizedAt: Midnight),
            CreateUpload(uploadId: 2, isReupload: true),
            CreateUpload(uploadId: 6, isReupload: false)
        );

        // Act
        var result = await service.GetRunningUploadsInStartOrderAsync(CancellationToken.None);

        // Assert
        result.Select(upload => upload.UploadId).ShouldBe([5, 4, 6, 2, 3]);
    }

    private static RunningUploadReadService CreateService(
        UploadStartOrder uploadStartOrder,
        params RunningUploadReadModel[] runningUploads
    )
    {
        var repositoryMock = new Mock<IUploadReadRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(repository => repository.GetRunningUploadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(runningUploads);

        var configurationProviderMock = new Mock<IApplicationConfigurationProvider>(
            MockBehavior.Strict
        );
        configurationProviderMock
            .Setup(provider =>
                provider.GetValue(
                    It.IsAny<Expression<Func<UploadConcurrencyConfiguration, UploadStartOrder>>>()
                )
            )
            .Returns(uploadStartOrder);

        return new RunningUploadReadService(
            repositoryMock.Object,
            configurationProviderMock.Object
        );
    }

    private static RunningUploadReadModel CreateUpload(
        int uploadId,
        bool isReupload,
        DateTime? prioritizedAt = null
    )
    {
        return new RunningUploadReadModel(
            UploadId: uploadId,
            ReleaseId: uploadId,
            ReleaseName: $"Release {uploadId}",
            UploadConfigName: "Upload config",
            HosterRegistrationName: "Hoster",
            HosterAccountPoolName: null,
            UploadState: UploadState.Pending,
            PrioritizedAt: prioritizedAt,
            IsReupload: isReupload,
            Files: []
        );
    }
}
