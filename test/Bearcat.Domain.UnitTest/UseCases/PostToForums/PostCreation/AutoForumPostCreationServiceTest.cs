using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.PostCreation;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums.PostCreation;

public class AutoForumPostCreationServiceTest
{
    [Test]
    public async Task CreatePostOnSiteAsync_ReplyModeWithOneMatchingThread_RepliesToMatchingThread()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread)
        );
        var submitter = new FakeForumPostSubmitter
        {
            ExistingThreads =
            [
                new ExistingThread("Other Movie 2020 720p", "https://forum.test/threads/other"),
                new ExistingThread(
                    "Some Movie 2021 1080p BluRay GROUP",
                    "https://forum.test/threads/matching"
                ),
            ],
        };
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Posted);
        result.PostedUrl.ShouldBe(submitter.ReplyUrl);
        submitter.NewThreads.ShouldBeEmpty();
        submitter
            .SearchedReleaseNames.ShouldHaveSingleItem()
            .ShouldBe("Some Movie 2021 1080p BluRay - GROUP");

        var reply = submitter.Replies.ShouldHaveSingleItem();
        reply.RegistrationId.ShouldBe(5);
        reply.ThreadUrl.ShouldBe("https://forum.test/threads/matching");
        reply.Body.ShouldBe("Rendered body");
    }

    [Test]
    public async Task CreatePostOnSiteAsync_ReplyModeWithSeveralMatchingThreads_ReturnsCandidatesWithoutPosting()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread)
        );
        var submitter = new FakeForumPostSubmitter
        {
            ExistingThreads =
            [
                new ExistingThread(
                    "Some Movie 2021 1080p BluRay GROUP PROPER",
                    "https://forum.test/threads/1"
                ),
                new ExistingThread(
                    "Some Movie 2021 1080p BluRay GROUP REPACK",
                    "https://forum.test/threads/2"
                ),
            ],
        };
        var contentRenderer = new FakeForumPostContentRenderer();
        var service = ServiceWith(repository, submitter, contentRenderer);

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.ThreadSelectionNeeded);
        result
            .ThreadCandidates.Select(thread => thread.Url)
            .ShouldBe(["https://forum.test/threads/1", "https://forum.test/threads/2"]);
        submitter.NewThreads.ShouldBeEmpty();
        submitter.Replies.ShouldBeEmpty();
        repository.RecordedPostedLocations.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostWithChosenThreadAsync_ThreadUrl_RepliesWithoutSearching()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread)
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        var plan = await new AutoForumPostingPlanService(repository).GetPlanAsync(
            ForumPostTemplateType.Release,
            1
        );

        // Act
        var result = await service.CreatePostWithChosenThreadAsync(
            plan: plan,
            site: plan.FindSite(5)!,
            threadUrl: "https://forum.test/threads/chosen"
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Posted);
        submitter.SearchedReleaseNames.ShouldBeEmpty();
        submitter
            .Replies.ShouldHaveSingleItem()
            .ThreadUrl.ShouldBe("https://forum.test/threads/chosen");
        repository.RecordedPostedLocations.ShouldHaveSingleItem().Url.ShouldBe(submitter.ReplyUrl);
    }

    [Test]
    public async Task CreatePostWithChosenThreadAsync_PostedMeanwhile_SkipsWithoutPosting()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        var plan = await new AutoForumPostingPlanService(repository).GetPlanAsync(
            ForumPostTemplateType.Release,
            1
        );

        repository.PostedLocations.Add(AutoPostTestFactory.PostedLocation());

        // Act
        var result = await service.CreatePostWithChosenThreadAsync(
            plan: plan,
            site: plan.FindSite(5)!,
            threadUrl: null
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.AlreadyPosted);
        submitter.NewThreads.ShouldBeEmpty();
        submitter.Replies.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_ReplyModeWithoutExistingThread_StartsNewThread()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(
                postMode: ForumPostPostMode.ReplyToExistingElseNewThread,
                threadPrefixIds: ["11", "23"]
            )
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Posted);
        result.PostedUrl.ShouldBe(submitter.NewThreadUrl);
        submitter.Replies.ShouldBeEmpty();

        var newThread = submitter.NewThreads.ShouldHaveSingleItem();
        newThread.TargetNodeId.ShouldBe("42");
        newThread.Title.ShouldBe("Some Movie 2021 1080p BluRay - GROUP");
        newThread.PrefixIds.ShouldBe(["11", "23"]);
    }

    [Test]
    public async Task CreatePostOnSiteAsync_AlwaysNewThread_DoesNotSearchForExistingThreads()
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
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Posted);
        submitter.SearchedReleaseNames.ShouldBeEmpty();
        submitter.NewThreads.ShouldHaveSingleItem().PrefixIds.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_SuccessfulPost_RecordsPostedLocation()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        var postedLocation = repository.RecordedPostedLocations.ShouldHaveSingleItem();
        postedLocation.EntityId.ShouldBe(1);
        postedLocation.DistributionSiteRegistrationId.ShouldBe(5);
        postedLocation.Url.ShouldBe(submitter.NewThreadUrl);
    }

    [Test]
    public async Task CreatePostOnSiteAsync_LastPendingSite_MarksReleasePosted()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.UploadsMarkedPosted.ShouldBeTrue();
        repository.MarkedReleaseIds.ShouldHaveSingleItem().ShouldBe(1);
    }

    [Test]
    public async Task CreatePostOnSiteAsync_AnotherSiteStillPending_DoesNotMarkReleasePosted()
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
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Posted);
        result.UploadsMarkedPosted.ShouldBeFalse();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_RenderErrors_FailsWithoutSubmitting()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(
            repository: repository,
            submitter: submitter,
            contentRenderer: new FakeForumPostContentRenderer { Errors = ["Unknown variable"] }
        );

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Failed);
        result.Errors.ShouldBe(["Unknown variable"]);
        submitter.NewThreads.ShouldBeEmpty();
        submitter.Replies.ShouldBeEmpty();
        repository.RecordedPostedLocations.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_SubmitterThrows_FailsWithExceptionMessage()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter
        {
            ThrowOnSubmit = new InvalidOperationException("Login failed"),
        };
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Failed);
        result.Errors.ShouldBe(["Login failed"]);
        repository.RecordedPostedLocations.ShouldBeEmpty();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_SubmitterThrowsAfterCancellation_PropagatesTheCancellation()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter
        {
            ThrowOnSubmit = new OperationCanceledException(),
        };
        var service = ServiceWith(repository, submitter);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var createPost = () =>
            service.CreatePostOnSiteAsync(
                entityType: ForumPostTemplateType.Release,
                entityId: 1,
                distributionSiteRegistrationId: 5,
                cancellationToken: cancellationTokenSource.Token
            );

        // Assert
        await createPost.ShouldThrowAsync<OperationCanceledException>();
        repository.RecordedPostedLocations.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_AlreadyPosted_IsSkipped()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        repository.PostedLocations.Add(AutoPostTestFactory.PostedLocation());
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.AlreadyPosted);
        submitter.NewThreads.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_NoMatchingRule_IsSkipped()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(resolution: ReleaseResolution.R720p)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.NoMatch);
    }

    [Test]
    public async Task CreatePostOnSiteAsync_BlockedRelease_IsSkippedWithBlockedReason()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(
            repository: repository,
            submitter: new FakeForumPostSubmitter(),
            notificationService: notificationService
        );

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.Blocked);
        result.BlockedReason.ShouldBe(AutoPostBlockedReason.QualityGateNotPassed);
        notificationService.Created.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_UnknownRegistration_IsSkipped()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 99
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.SiteNotConfigured);
    }

    [Test]
    public async Task CreatePostOnSiteAsync_StripDotsDisabled_SearchesWithTheRawReleaseName()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread),
            stripDotsForThreadSearch: false
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        submitter
            .SearchedReleaseNames.ShouldHaveSingleItem()
            .ShouldBe(AutoPostTestFactory.ReleaseName);
    }

    [Test]
    public async Task CreatePostOnSiteAsync_StripDotsEnabled_UsesTheSearchedNameAsNewThreadTitle()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread)
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        submitter
            .SearchedReleaseNames.ShouldHaveSingleItem()
            .ShouldBe("Some Movie 2021 1080p BluRay - GROUP");
        submitter
            .NewThreads.ShouldHaveSingleItem()
            .Title.ShouldBe("Some Movie 2021 1080p BluRay - GROUP");
    }

    [Test]
    public async Task CreatePostOnSiteAsync_Collection_UsesTheCollectionNameAndRecordsACollectionPostedLocation()
    {
        // Arrange
        var repository = CollectionRepository();
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.ReleaseCollection,
            entityId: 4,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Posted);
        result.UploadsMarkedPosted.ShouldBeTrue();
        submitter
            .NewThreads.ShouldHaveSingleItem()
            .Title.ShouldBe("Some Show S01 1080p WEB - GROUP");
        repository.ReleaseCollectionPostedLocations.ShouldHaveSingleItem().EntityId.ShouldBe(4);
        repository.PostedLocations.ShouldBeEmpty();
        repository.MarkedReleaseCollectionIds.ShouldBe([4]);
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePostOnSiteAsync_SiteWithRecordedFailures_DeletesTheFailureRecord()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        repository.ForumPostingFailures.Add(
            new ForumPostingFailure
            {
                ReleaseId = 1,
                DistributionSiteRegistrationId = 5,
                FailedAttemptCount = 5,
                LastErrorMessage = "Flood control",
                State = ForumPostingFailureState.GaveUp,
            }
        );
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.CreatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostResultStatus.Posted);
        repository.ForumPostingFailures.ShouldBeEmpty();
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

    private static FakeAutoForumPostingRepository CollectionRepository()
    {
        return new FakeAutoForumPostingRepository
        {
            ReleaseCollections = [AutoPostTestFactory.ReleaseCollection(id: 4)],
            Registrations =
            [
                AutoPostTestFactory.Registration(
                    id: 5,
                    name: "Board",
                    rules: AutoPostTestFactory.Rule(
                        templateType: ForumPostTemplateType.ReleaseCollection
                    )
                ),
            ],
        };
    }

    private static AutoForumPostCreationService ServiceWith(
        FakeAutoForumPostingRepository repository,
        FakeForumPostSubmitter submitter,
        FakeForumPostContentRenderer? contentRenderer = null,
        FakeNotificationService? notificationService = null
    )
    {
        var planService = new AutoForumPostingPlanService(repository);

        return new AutoForumPostCreationService(
            planService: planService,
            repository: repository,
            contentRenderer: contentRenderer ?? new FakeForumPostContentRenderer(),
            submitter: submitter,
            notificationService: notificationService ?? new FakeNotificationService(),
            uploadsPostedStatusService: new UploadsPostedStatusService(planService, repository)
        );
    }
}
