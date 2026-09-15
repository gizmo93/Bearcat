using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public class AutoForumPostingExecutionServiceTest
{
    [Test]
    public async Task ExecuteAsync_ReplyModeWithExistingThread_RepliesToFirstThread()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread)
        );
        var submitter = new FakeForumPostSubmitter
        {
            ExistingThreads =
            [
                new ExistingThread("First", "https://forum.test/threads/first"),
                new ExistingThread("Second", "https://forum.test/threads/second"),
            ],
        };
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Posted);
        result.PostedUrl.ShouldBe(submitter.ReplyUrl);
        submitter.NewThreads.ShouldBeEmpty();
        submitter
            .SearchedReleaseNames.ShouldHaveSingleItem()
            .ShouldBe("Some Movie 2021 1080p BluRay - GROUP");

        var reply = submitter.Replies.ShouldHaveSingleItem();
        reply.RegistrationId.ShouldBe(5);
        reply.ThreadUrl.ShouldBe("https://forum.test/threads/first");
        reply.Body.ShouldBe("Rendered body");
    }

    [Test]
    public async Task ExecuteAsync_ReplyModeWithoutExistingThread_StartsNewThread()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(
                postMode: ForumPostPostMode.ReplyToExistingElseNewThread,
                threadPrefixId: "11"
            )
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Posted);
        result.PostedUrl.ShouldBe(submitter.NewThreadUrl);
        submitter.Replies.ShouldBeEmpty();

        var newThread = submitter.NewThreads.ShouldHaveSingleItem();
        newThread.TargetNodeId.ShouldBe("42");
        newThread.Title.ShouldBe("Some Movie 2021 1080p BluRay - GROUP");
        newThread.PrefixIds.ShouldBe(["11"]);
    }

    [Test]
    public async Task ExecuteAsync_AlwaysNewThread_DoesNotSearchForExistingThreads()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.AlwaysNewThread)
        );
        var submitter = new FakeForumPostSubmitter
        {
            ExistingThreads = [new ExistingThread("First", "https://forum.test/threads/first")],
        };
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Posted);
        submitter.SearchedReleaseNames.ShouldBeEmpty();
        submitter.NewThreads.ShouldHaveSingleItem().PrefixIds.ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_SuccessfulPost_RecordsPostedLocation()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        var postedLocation = repository.RecordedPostedLocations.ShouldHaveSingleItem();
        postedLocation.ReleaseId.ShouldBe(1);
        postedLocation.DistributionSiteRegistrationId.ShouldBe(5);
        postedLocation.Url.ShouldBe(submitter.NewThreadUrl);
    }

    [Test]
    public async Task ExecuteAsync_LastPendingSite_MarksReleasePosted()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.ReleaseMarkedPosted.ShouldBeTrue();
        repository.MarkedReleaseIds.ShouldHaveSingleItem().ShouldBe(1);
    }

    [Test]
    public async Task ExecuteAsync_AnotherSiteStillPending_DoesNotMarkReleasePosted()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations =
            [
                AutoPostTestFactory.Registration(id: 5, name: "Board", AutoPostTestFactory.Rule()),
                AutoPostTestFactory.Registration(id: 6, name: "Other", AutoPostTestFactory.Rule()),
            ],
        };
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Posted);
        result.ReleaseMarkedPosted.ShouldBeFalse();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_RenderErrors_FailsWithoutSubmitting()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter();
        var service = new AutoForumPostingExecutionService(
            new AutoForumPostingPlanService(repository),
            repository,
            new FakeForumPostContentRenderer { Errors = ["Unknown variable"] },
            submitter,
            new FakeNotificationService()
        );

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Failed);
        result.Errors.ShouldBe(["Unknown variable"]);
        submitter.NewThreads.ShouldBeEmpty();
        submitter.Replies.ShouldBeEmpty();
        repository.RecordedPostedLocations.ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_SubmitterThrows_FailsWithExceptionMessage()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter
        {
            ThrowOnSubmit = new InvalidOperationException("Login failed"),
        };
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Failed);
        result.Errors.ShouldBe(["Login failed"]);
        repository.RecordedPostedLocations.ShouldBeEmpty();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_AlreadyPosted_IsSkipped()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        repository.PostedLocations.Add(
            new AutoPostPostedLocation(1, 5, "https://forum.test/threads/1")
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.AlreadyPosted);
        submitter.NewThreads.ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_NoMatchingRule_IsSkipped()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(resolution: ReleaseResolution.R720p)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.NoMatch);
    }

    [Test]
    public async Task ExecuteAsync_BlockedRelease_IsSkippedWithBlockedReason()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.Blocked);
        result.BlockedReason.ShouldBe(AutoPostBlockedReason.QualityGateNotPassed);
    }

    [Test]
    public async Task ExecuteAsync_UnknownRegistration_IsSkipped()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 99);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.SiteNotConfigured);
    }

    [Test]
    public async Task ExecuteAsync_StripDotsDisabled_SearchesWithTheRawReleaseName()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread),
            stripDotsForThreadSearch: false
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        submitter
            .SearchedReleaseNames.ShouldHaveSingleItem()
            .ShouldBe(AutoPostTestFactory.ReleaseName);
    }

    [Test]
    public async Task ExecuteAsync_StripDotsEnabled_UsesTheSearchedNameAsNewThreadTitle()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread)
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        submitter
            .SearchedReleaseNames.ShouldHaveSingleItem()
            .ShouldBe("Some Movie 2021 1080p BluRay - GROUP");
        submitter
            .NewThreads.ShouldHaveSingleItem()
            .Title.ShouldBe("Some Movie 2021 1080p BluRay - GROUP");
    }

    [Test]
    public async Task ExecuteAsync_SkippedRelease_CreatesNoNotification()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(repository, new FakeForumPostSubmitter(), notificationService);

        // Act
        await service.ExecuteAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        notificationService.Created.ShouldBeEmpty();
    }

    private static FakeAutoForumPostingRepository RepositoryWith(
        ForumPostingRule rule,
        bool stripDotsForThreadSearch = true
    )
    {
        return new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations =
            [
                AutoPostTestFactory.Registration(id: 5, name: "Board", rule) with
                {
                    StripDotsForThreadSearch = stripDotsForThreadSearch,
                },
            ],
        };
    }

    private static AutoForumPostingExecutionService ServiceWith(
        FakeAutoForumPostingRepository repository,
        FakeForumPostSubmitter submitter,
        FakeNotificationService? notificationService = null
    )
    {
        return new AutoForumPostingExecutionService(
            new AutoForumPostingPlanService(repository),
            repository,
            new FakeForumPostContentRenderer(),
            submitter,
            notificationService ?? new FakeNotificationService()
        );
    }
}
