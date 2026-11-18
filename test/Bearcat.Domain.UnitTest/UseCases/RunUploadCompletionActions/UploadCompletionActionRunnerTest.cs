using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.Security;
using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UnitTest.UseCases.PostToForums;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.RunUploadCompletionActions;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UnitTest.UseCases.RunUploadCompletionActions;

public class UploadCompletionActionRunnerTest
{
    private const int ReleaseId = 7;

    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Unspecified);

    private static readonly DateTime RegistrationCreatedAt = new(
        2026,
        11,
        1,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private static readonly DateTime PreviouslyHandledUploadedAt = new(
        2026,
        11,
        10,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private static readonly DateTime NewestUploadedAt = new(
        2026,
        11,
        18,
        11,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private readonly UploadCompletionActionRegistration registration = new()
    {
        Id = 3,
        Name = "Export",
        ActionClassName = "TestUploadCompletionAction",
        SerializedConfig = "encrypted",
        IsActive = true,
        RunForInitialUpload = true,
        RunForReupload = true,
        CreatedAt = RegistrationCreatedAt,
    };

    private Mock<IUploadCompletionAction> action = null!;

    private Mock<IUploadCompletionActionRepository> repository = null!;

    private FakeNotificationService notificationService = null!;

    [SetUp]
    public void SetUp()
    {
        action = new Mock<IUploadCompletionAction>();
        repository = new Mock<IUploadCompletionActionRepository>();
        notificationService = new FakeNotificationService();
    }

    [Test]
    public async Task RunAsync_ActionSucceeds_MovesHandledUploadedAtAndClearsErrorMessage()
    {
        // Arrange
        var execution = Execution(errorMessage: "Previous failure");
        SetUpRepository(execution);

        action
            .Setup(uploadCompletionAction =>
                uploadCompletionAction.ExecuteAsync(
                    It.IsAny<IUploadCompletionActionConfig>(),
                    It.IsAny<UploadCompletionActionInput>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("Exported");

        // Act
        await CreateRunner().RunAsync();

        // Assert
        execution.HandledUploadedAt.ShouldBe(NewestUploadedAt);
        execution.ExecutedAt.ShouldBe(Now);
        execution.ResultText.ShouldBe("Exported");
        execution.ErrorMessage.ShouldBeNull();
        notificationService.Created.ShouldBeEmpty();
        repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunAsync_ActionFailsWithoutPreviousExecution_KeepsHandledUploadedAtStoresErrorAndCreatesNotification()
    {
        // Arrange
        SetUpRepository(execution: null);
        UploadCompletionActionExecution? addedExecution = null;

        repository
            .Setup(repo => repo.AddExecution(It.IsAny<UploadCompletionActionExecution>()))
            .Callback<UploadCompletionActionExecution>(execution => addedExecution = execution);

        SetUpFailingAction("Target folder is missing");

        // Act
        await CreateRunner().RunAsync();

        // Assert
        addedExecution.ShouldNotBeNull();
        addedExecution.ReleaseId.ShouldBe(ReleaseId);
        addedExecution.UploadCompletionActionRegistrationId.ShouldBe(registration.Id);
        addedExecution.HandledUploadedAt.ShouldBe(RegistrationCreatedAt);
        addedExecution.ExecutedAt.ShouldBe(Now);
        addedExecution.ErrorMessage.ShouldBe("Target folder is missing");

        notificationService
            .Created.ShouldHaveSingleItem()
            .Kind.ShouldBe(NotificationKind.UploadCompletionActionFailed);
    }

    [Test]
    public async Task RunAsync_ActionFailsAgain_KeepsHandledUploadedAtAndCreatesNoSecondNotification()
    {
        // Arrange
        var execution = Execution(errorMessage: "Target folder is missing");
        SetUpRepository(execution);
        SetUpFailingAction("Target folder is still missing");

        // Act
        await CreateRunner().RunAsync();

        // Assert
        execution.HandledUploadedAt.ShouldBe(PreviouslyHandledUploadedAt);
        execution.ExecutedAt.ShouldBe(Now);
        execution.ErrorMessage.ShouldBe("Target folder is still missing");
        notificationService.Created.ShouldBeEmpty();
    }

    [Test]
    public async Task RunForReleaseAsync_ActionFailsWithoutPreviousFailure_StoresErrorAndCreatesNoNotification()
    {
        // Arrange
        var execution = Execution(errorMessage: null);
        SetUpRepository(execution);

        repository
            .Setup(repo =>
                repo.GetRegistrationAsync(registration.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(registration);

        repository
            .Setup(repo =>
                repo.GetCompletelyUploadedReleaseAsync(ReleaseId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new UploadCompletionActionDueRelease(
                    ReleaseId: ReleaseId,
                    IsReupload: false,
                    NewestUploadedAt: NewestUploadedAt
                )
            );

        SetUpFailingAction("Target folder is missing");

        // Act
        await CreateRunner().RunForReleaseAsync(registration.Id, ReleaseId);

        // Assert
        execution.HandledUploadedAt.ShouldBe(PreviouslyHandledUploadedAt);
        execution.ExecutedAt.ShouldBe(Now);
        execution.ErrorMessage.ShouldBe("Target folder is missing");
        notificationService.Created.ShouldBeEmpty();
    }

    private static UploadCompletionActionExecution Execution(string? errorMessage)
    {
        return new UploadCompletionActionExecution
        {
            ReleaseId = ReleaseId,
            UploadCompletionActionRegistrationId = 3,
            HandledUploadedAt = PreviouslyHandledUploadedAt,
            ExecutedAt = PreviouslyHandledUploadedAt,
            ErrorMessage = errorMessage,
        };
    }

    private void SetUpFailingAction(string message)
    {
        action
            .Setup(uploadCompletionAction =>
                uploadCompletionAction.ExecuteAsync(
                    It.IsAny<IUploadCompletionActionConfig>(),
                    It.IsAny<UploadCompletionActionInput>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException(message));
    }

    private void SetUpRepository(UploadCompletionActionExecution? execution)
    {
        repository
            .Setup(repo =>
                repo.GetActiveRegistrationsWithReadableSecretsAsync(It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([registration]);

        repository
            .Setup(repo => repo.GetDueReleasesAsync(registration, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UploadCompletionActionDueRelease(
                    ReleaseId: ReleaseId,
                    IsReupload: true,
                    NewestUploadedAt: NewestUploadedAt
                ),
            ]);

        repository
            .Setup(repo =>
                repo.GetExecutionAsync(ReleaseId, registration.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(execution);
    }

    private static IReleaseReadRepository CreateReleaseReadRepository()
    {
        var releaseReadRepository = new Mock<IReleaseReadRepository>
        {
            DefaultValueProvider = DefaultValueProvider.Mock,
        };

        releaseReadRepository
            .Setup(repo =>
                repo.GetReleasesByIdAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<int, ReleaseReadModel>
                {
                    {
                        ReleaseId,
                        new ReleaseReadModel(
                            ReleaseId: ReleaseId,
                            Name: "Bearcat.Release.001",
                            ReleaseType: ReleaseType.Managed,
                            ReleaseContentType: ReleaseContentType.Other,
                            PrimaryLanguageCode: null,
                            ReleaseGroupId: 1,
                            ReleaseGroupName: "Bearcat",
                            ReleaseFolderPath: null,
                            UploadConfigsCount: 0,
                            OnlineUploadConfigsCount: 0,
                            ExcludeFromAutoCleanup: false,
                            UploadsPostedAt: null,
                            HasUploadConfigsWaitingForInitialUpload: false,
                            CreateScreenshots: false,
                            ScreenshotCount: 0,
                            CreateScreenshotGrid: false,
                            ScreenshotGridColumns: 0,
                            ScreenshotGridRows: 0
                        )
                    },
                }
            );

        return releaseReadRepository.Object;
    }

    private UploadCompletionActionRunner CreateRunner()
    {
        action
            .Setup(uploadCompletionAction => uploadCompletionAction.DeserializeConfig("decrypted"))
            .Returns(Mock.Of<IUploadCompletionActionConfig>());

        var factory = new Mock<IUploadCompletionActionFactory>();

        factory
            .Setup(uploadCompletionActionFactory =>
                uploadCompletionActionFactory.GetByClassName(registration.ActionClassName)
            )
            .Returns(action.Object);

        var secretProtector = new Mock<ISecretProtector>();

        secretProtector
            .Setup(protector => protector.Unprotect(registration.SerializedConfig))
            .Returns("decrypted");

        var uploadRepository = new Mock<IReleaseForumPostUploadRepository>();

        uploadRepository
            .Setup(repo =>
                repo.GetForumPostUploadsByReleaseIdAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<int, IReadOnlyList<ReleaseForumPostUploadReadModel>>());

        uploadRepository
            .Setup(repo =>
                repo.GetForumPostAllHostersLinkCryptersByReleaseIdAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<int, IReadOnlyList<ReleaseForumPostLinkCrypterReadModel>>()
            );

        var imageLinkRepository = new Mock<IForumPostImageLinkRepository>();

        imageLinkRepository
            .Setup(repo =>
                repo.GetReleaseImageLinksByReleaseIdAsync(
                    It.IsAny<IReadOnlyList<int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<int, IReadOnlyList<ForumPostImageLinkReadModel>>());

        var inputBuilder = new UploadCompletionActionInputBuilder(
            releaseForumPostRenderModelBuilder: new ReleaseForumPostRenderModelBuilder(
                releaseReadRepository: CreateReleaseReadRepository(),
                uploadRepository: uploadRepository.Object,
                imageLinkRepository: imageLinkRepository.Object
            ),
            imageHosterFactory: Mock.Of<IImageHosterFactory>(),
            linkCrypterFactory: Mock.Of<ILinkCrypterFactory>()
        );

        var timeProvider = new Mock<TimeProvider>(new ConfigurationBuilder().Build());
        timeProvider.Setup(provider => provider.GetLocalNow()).Returns(Now);

        return new UploadCompletionActionRunner(
            repository: repository.Object,
            uploadCompletionActionFactory: factory.Object,
            secretProtector: secretProtector.Object,
            inputBuilder: inputBuilder,
            notificationService: notificationService,
            timeProvider: timeProvider.Object,
            logger: NullLogger<UploadCompletionActionRunner>.Instance
        );
    }
}
