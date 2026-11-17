using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.PostCreation;
using Bearcat.Domain.UseCases.PostToForums.PostUpdate;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public class AutoForumPostingRunnerTest
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Unspecified);

    [Test]
    public async Task RunAsync_MixedRegistrations_PostsOnlyToAutomaticallyEnabledSites()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations =
            [
                AutomaticRegistration(id: 5, name: "Automatic"),
                AutoPostTestFactory.Registration(id: 6, name: "Manual", AutoPostTestFactory.Rule()),
            ],
        };
        var submitter = new FakeForumPostSubmitter();
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.PostedCount.ShouldBe(1);
        result.FailedCount.ShouldBe(0);
        submitter.NewThreads.ShouldHaveSingleItem().RegistrationId.ShouldBe(5);
        repository
            .RecordedPostedLocations.ShouldHaveSingleItem()
            .DistributionSiteRegistrationId.ShouldBe(5);
    }

    [Test]
    public async Task RunAsync_BlockedRelease_PostsNothing()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)],
            Registrations = [AutomaticRegistration(id: 5, name: "Automatic")],
        };
        var submitter = new FakeForumPostSubmitter();
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.PostedCount.ShouldBe(0);
        result.Failures.ShouldBeEmpty();
        submitter.NewThreads.ShouldBeEmpty();
    }

    [Test]
    public async Task RunAsync_OneFailingSite_KeepsPostingToTheRemainingSites()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations =
            [
                AutomaticRegistration(id: 5, name: "Broken"),
                AutomaticRegistration(id: 6, name: "Working"),
            ],
        };
        var submitter = new FakeForumPostSubmitter { FailingRegistrationIds = [5] };
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.PostedCount.ShouldBe(1);
        result.FailedCount.ShouldBe(1);
        submitter.NewThreads.ShouldHaveSingleItem().RegistrationId.ShouldBe(6);

        var failure = result.Failures.ShouldHaveSingleItem();
        failure.EntityType.ShouldBe(ForumPostTemplateType.Release);
        failure.EntityId.ShouldBe(1);
        failure.EntityName.ShouldBe(AutoPostTestFactory.ReleaseName);
        failure.DistributionSiteRegistrationId.ShouldBe(5);
        failure.DistributionSiteRegistrationName.ShouldBe("Broken");
        failure.Messages.ShouldBe(["Submit to 5 failed"]);
    }

    [Test]
    public async Task RunAsync_MoreQueuedReleasesThanOneBatch_PlansReleasesInBatchesOfFiveHundred()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = Enumerable
                .Range(1, 1001)
                .Select(id =>
                    AutoPostTestFactory.Release(id: id, qualityGateState: QualityGateState.Failed)
                )
                .ToList(),
            Registrations = [AutomaticRegistration(id: 5, name: "Automatic")],
        };
        var runner = RunnerWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.PostedCount.ShouldBe(0);
        repository.PlanningRoundCount.ShouldBe(3);
    }

    [Test]
    public async Task RunAsync_StalePostOnAnAutomaticSite_UpdatesItAndMarksTheReleasePosted()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutomaticRegistration(id: 5, name: "Automatic")],
            PostedLocations =
            [
                AutoPostTestFactory.PostedLocation(postedLocationId: 9, forumPostTemplateId: 3),
            ],
            LatestUploadCompletionTimes = { [1] = AutoPostTestFactory.ReuploadedAt },
            Now = AutoPostTestFactory.ReuploadedAt.AddHours(1),
        };
        var submitter = new FakeForumPostSubmitter();
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.UpdatedCount.ShouldBe(1);
        result.PostedCount.ShouldBe(0);
        result.FailedCount.ShouldBe(0);
        submitter.EditedPosts.ShouldHaveSingleItem().RegistrationId.ShouldBe(5);
        repository.UpdatedPostedLocations.ShouldHaveSingleItem().PostedLocationId.ShouldBe(9);
        repository.MarkedReleaseIds.ShouldBe([1]);
    }

    [Test]
    public async Task RunAsync_StalePostOnAManualSite_UpdatesNothingAndKeepsTheReleaseQueued()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5, name: "Manual")],
            PostedLocations =
            [
                AutoPostTestFactory.PostedLocation(postedLocationId: 9, forumPostTemplateId: 3),
            ],
            LatestUploadCompletionTimes = { [1] = AutoPostTestFactory.ReuploadedAt },
        };
        var submitter = new FakeForumPostSubmitter();
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.UpdatedCount.ShouldBe(0);
        submitter.EditedPosts.ShouldBeEmpty();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task RunAsync_FailingUpdate_ReportsAFailureAndKeepsTheReleaseInTheQueue()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutomaticRegistration(id: 5, name: "Automatic")],
            PostedLocations =
            [
                AutoPostTestFactory.PostedLocation(postedLocationId: 9, forumPostTemplateId: 3),
            ],
            LatestUploadCompletionTimes = { [1] = AutoPostTestFactory.ReuploadedAt },
        };
        var submitter = new FakeForumPostSubmitter { FailingRegistrationIds = [5] };
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.UpdatedCount.ShouldBe(0);
        result.Failures.ShouldHaveSingleItem().DistributionSiteRegistrationId.ShouldBe(5);
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task RunAsync_NoRuleMatchesAnywhere_DoesNotMarkTheReleasePosted()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(resolution: ReleaseResolution.R720p)],
            Registrations = [AutomaticRegistration(id: 5, name: "Automatic")],
        };
        var submitter = new FakeForumPostSubmitter();
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.PostedCount.ShouldBe(0);
        result.UpdatedCount.ShouldBe(0);
        submitter.NewThreads.ShouldBeEmpty();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task RunAsync_StaleAutomaticSiteAndStaleManualSite_DoesNotMarkTheReleasePosted()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations =
            [
                AutomaticRegistration(id: 5, name: "Automatic"),
                AutoPostTestFactory.Registration(id: 6, name: "Manual"),
            ],
            PostedLocations =
            [
                AutoPostTestFactory.PostedLocation(postedLocationId: 9, forumPostTemplateId: 3),
                AutoPostTestFactory.PostedLocation(
                    postedLocationId: 10,
                    distributionSiteRegistrationId: 6,
                    forumPostTemplateId: 3
                ),
            ],
            LatestUploadCompletionTimes = { [1] = AutoPostTestFactory.ReuploadedAt },
            Now = AutoPostTestFactory.ReuploadedAt.AddHours(1),
        };
        var runner = RunnerWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.UpdatedCount.ShouldBe(1);
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task RunAsync_QueuedReleaseAndCollection_PostsBothWithTheirOwnRules()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(id: 1)],
            ReleaseCollections = [AutoPostTestFactory.ReleaseCollection(id: 4)],
            Registrations =
            [
                AutoPostTestFactory.Registration(
                    id: 5,
                    name: "Automatic",
                    rules:
                    [
                        AutoPostTestFactory.Rule(id: 1, forumPostTemplateId: 7),
                        AutoPostTestFactory.Rule(
                            id: 2,
                            forumPostTemplateId: 8,
                            templateType: ForumPostTemplateType.ReleaseCollection
                        ),
                    ]
                ) with
                {
                    EnableAutomaticPosting = true,
                },
            ],
        };
        var submitter = new FakeForumPostSubmitter();
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.PostedCount.ShouldBe(2);
        result.FailedCount.ShouldBe(0);
        submitter
            .NewThreads.Select(thread => thread.Title)
            .ShouldBe(["Some Movie 2021 1080p BluRay - GROUP", "Some Show S01 1080p WEB - GROUP"]);
        repository.PostedLocations.ShouldHaveSingleItem().ForumPostTemplateId.ShouldBe(7);
        repository
            .ReleaseCollectionPostedLocations.ShouldHaveSingleItem()
            .ForumPostTemplateId.ShouldBe(8);
        repository.MarkedReleaseIds.ShouldBe([1]);
        repository.MarkedReleaseCollectionIds.ShouldBe([4]);
    }

    [Test]
    public async Task RunAsync_SiteWithRetryScheduledInTheFuture_SkipsTheSite()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutomaticRegistration(id: 5, name: "Automatic")],
            ForumPostingFailures =
            [
                RetryScheduledFailure(failedAttemptCount: 1, nextAttemptAt: Now.AddMinutes(10)),
            ],
        };
        var submitter = new FakeForumPostSubmitter();
        var runner = RunnerWith(repository, submitter);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.PostedCount.ShouldBe(0);
        result.Failures.ShouldBeEmpty();
        submitter.NewThreads.ShouldBeEmpty();
        repository.ForumPostingFailures.ShouldHaveSingleItem().FailedAttemptCount.ShouldBe(1);
    }

    [Test]
    public async Task RunAsync_FifthFailedAttempt_GivesUpWithOneNotification()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutomaticRegistration(id: 5, name: "Broken")],
            ForumPostingFailures =
            [
                RetryScheduledFailure(failedAttemptCount: 4, nextAttemptAt: Now.AddMinutes(-1)),
            ],
        };
        var submitter = new FakeForumPostSubmitter { FailingRegistrationIds = [5] };
        var notificationService = new FakeNotificationService();
        var runner = RunnerWith(repository, submitter, notificationService);

        // Act
        var result = await runner.RunAsync();

        // Assert
        result.FailedCount.ShouldBe(1);

        var failure = repository.ForumPostingFailures.ShouldHaveSingleItem();
        failure.State.ShouldBe(ForumPostingFailureState.GaveUp);
        failure.FailedAttemptCount.ShouldBe(5);
        failure.NextAttemptAt.ShouldBeNull();
        failure.LastErrorMessage.ShouldBe("Submit to 5 failed");

        var notification = notificationService.Created.ShouldHaveSingleItem();
        notification.Kind.ShouldBe(NotificationKind.AutomaticForumPostFailed);
        notification.Message.ShouldContain("5 failed attempts");
    }

    private static ForumPostingFailure RetryScheduledFailure(
        int failedAttemptCount,
        DateTime nextAttemptAt
    )
    {
        return new ForumPostingFailure
        {
            ReleaseId = 1,
            DistributionSiteRegistrationId = 5,
            FailedAttemptCount = failedAttemptCount,
            NextAttemptAt = nextAttemptAt,
            LastErrorMessage = "Submit to 5 failed",
            State = ForumPostingFailureState.RetryScheduled,
        };
    }

    private static AutoPostRegistration AutomaticRegistration(
        int id,
        string name,
        ForumPostingRule? rule = null
    )
    {
        return AutoPostTestFactory.Registration(id, name, rule ?? AutoPostTestFactory.Rule()) with
        {
            EnableAutomaticPosting = true,
        };
    }

    private static AutoForumPostingRunner RunnerWith(
        FakeAutoForumPostingRepository repository,
        FakeForumPostSubmitter submitter,
        FakeNotificationService? notificationService = null
    )
    {
        var planService = new AutoForumPostingPlanService(repository);
        var contentRenderer = new FakeForumPostContentRenderer();
        notificationService ??= new FakeNotificationService();
        var uploadsPostedStatusService = new UploadsPostedStatusService(planService, repository);

        var timeProvider = new Mock<TimeProvider>(new ConfigurationBuilder().Build());
        timeProvider.Setup(provider => provider.GetLocalNow()).Returns(Now);

        return new AutoForumPostingRunner(
            repository: repository,
            planService: planService,
            postCreationService: new AutoForumPostCreationService(
                planService: planService,
                repository: repository,
                contentRenderer: contentRenderer,
                submitter: submitter,
                notificationService: notificationService,
                uploadsPostedStatusService: uploadsPostedStatusService
            ),
            postUpdateService: new AutoForumPostUpdateService(
                planService: planService,
                repository: repository,
                contentRenderer: contentRenderer,
                submitter: submitter,
                notificationService: notificationService,
                uploadsPostedStatusService: uploadsPostedStatusService
            ),
            notificationService: notificationService,
            timeProvider: timeProvider.Object
        );
    }
}
