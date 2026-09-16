using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public class AutoForumPostingRunnerTest
{
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
        failure.ReleaseId.ShouldBe(1);
        failure.ReleaseName.ShouldBe(AutoPostTestFactory.ReleaseName);
        failure.DistributionSiteRegistrationId.ShouldBe(5);
        failure.DistributionSiteRegistrationName.ShouldBe("Broken");
        failure.Messages.ShouldBe(["Submit to 5 failed"]);
    }

    [Test]
    public async Task RunAsync_NoQueuedReleases_PostsNothing()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
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
    public async Task RunAsync_SuccessAndFailure_CreatesNotificationForBothOutcomes()
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
        var notificationService = new FakeNotificationService();
        var runner = RunnerWith(repository, submitter, notificationService);

        // Act
        await runner.RunAsync();

        // Assert
        notificationService.Created.Count.ShouldBe(2);

        var failed = notificationService.Created[0];
        failed.Kind.ShouldBe(NotificationKind.AutomaticForumPostFailed);
        failed.Message.ShouldBe(
            $"Automatic posting of release '{AutoPostTestFactory.ReleaseName}' to 'Broken' failed: Submit to 5 failed"
        );

        var created = notificationService.Created[1];
        created.Kind.ShouldBe(NotificationKind.AutomaticForumPostCreated);
        created.Message.ShouldBe(
            $"Release '{AutoPostTestFactory.ReleaseName}' was posted automatically to 'Working': {submitter.NewThreadUrl}"
        );
    }

    [Test]
    public async Task RunAsync_SuccessfulPost_PlansOnceUpFrontAndOnceAfterPosting()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutomaticRegistration(id: 5, name: "Automatic")],
        };
        var runner = RunnerWith(repository, new FakeForumPostSubmitter());

        // Act
        await runner.RunAsync();

        // Assert
        repository.PlanningRoundCount.ShouldBe(2);
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

        return new AutoForumPostingRunner(
            repository,
            planService,
            new AutoForumPostingService(
                planService,
                repository,
                new FakeForumPostContentRenderer(),
                submitter,
                notificationService ?? new FakeNotificationService()
            )
        );
    }
}
