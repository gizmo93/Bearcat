using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public class AutoForumPostingServiceTest
{
    [Test]
    public async Task PostAsync_ReplyModeWithExistingThread_RepliesToFirstThread()
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
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

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
    public async Task PostAsync_ReplyModeWithoutExistingThread_StartsNewThread()
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
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

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
    public async Task PostAsync_AlwaysNewThread_DoesNotSearchForExistingThreads()
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
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Posted);
        submitter.SearchedReleaseNames.ShouldBeEmpty();
        submitter.NewThreads.ShouldHaveSingleItem().PrefixIds.ShouldBeEmpty();
    }

    [Test]
    public async Task PostAsync_SuccessfulPost_RecordsPostedLocation()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        var postedLocation = repository.RecordedPostedLocations.ShouldHaveSingleItem();
        postedLocation.ReleaseId.ShouldBe(1);
        postedLocation.DistributionSiteRegistrationId.ShouldBe(5);
        postedLocation.Url.ShouldBe(submitter.NewThreadUrl);
    }

    [Test]
    public async Task PostAsync_LastPendingSite_MarksReleasePosted()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.ReleaseMarkedPosted.ShouldBeTrue();
        repository.MarkedReleaseIds.ShouldHaveSingleItem().ShouldBe(1);
    }

    [Test]
    public async Task PostAsync_AnotherSiteStillPending_DoesNotMarkReleasePosted()
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
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Posted);
        result.ReleaseMarkedPosted.ShouldBeFalse();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task PostAsync_RenderErrors_FailsWithoutSubmitting()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(
            repository,
            submitter,
            new FakeForumPostContentRenderer { Errors = ["Unknown variable"] }
        );

        // Act
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Failed);
        result.Errors.ShouldBe(["Unknown variable"]);
        submitter.NewThreads.ShouldBeEmpty();
        submitter.Replies.ShouldBeEmpty();
        repository.RecordedPostedLocations.ShouldBeEmpty();
    }

    [Test]
    public async Task PostAsync_SubmitterThrows_FailsWithExceptionMessage()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var submitter = new FakeForumPostSubmitter
        {
            ThrowOnSubmit = new InvalidOperationException("Login failed"),
        };
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Failed);
        result.Errors.ShouldBe(["Login failed"]);
        repository.RecordedPostedLocations.ShouldBeEmpty();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task PostAsync_AlreadyPosted_IsSkipped()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        repository.PostedLocations.Add(AutoPostTestFactory.PostedLocation());
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.AlreadyPosted);
        submitter.NewThreads.ShouldBeEmpty();
    }

    [Test]
    public async Task PostAsync_NoMatchingRule_IsSkipped()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(resolution: ReleaseResolution.R720p)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.NoMatch);
    }

    [Test]
    public async Task PostAsync_BlockedRelease_IsSkippedWithBlockedReason()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.Blocked);
        result.BlockedReason.ShouldBe(AutoPostBlockedReason.QualityGateNotPassed);
    }

    [Test]
    public async Task PostAsync_UnknownRegistration_IsSkipped()
    {
        // Arrange
        var repository = RepositoryWith(AutoPostTestFactory.Rule());
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 99);

        // Assert
        result.Status.ShouldBe(AutoPostExecutionStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostSkipReason.SiteNotConfigured);
    }

    [Test]
    public async Task PostAsync_StripDotsDisabled_SearchesWithTheRawReleaseName()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread),
            stripDotsForThreadSearch: false
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        submitter
            .SearchedReleaseNames.ShouldHaveSingleItem()
            .ShouldBe(AutoPostTestFactory.ReleaseName);
    }

    [Test]
    public async Task PostAsync_StripDotsEnabled_UsesTheSearchedNameAsNewThreadTitle()
    {
        // Arrange
        var repository = RepositoryWith(
            AutoPostTestFactory.Rule(postMode: ForumPostPostMode.ReplyToExistingElseNewThread)
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        submitter
            .SearchedReleaseNames.ShouldHaveSingleItem()
            .ShouldBe("Some Movie 2021 1080p BluRay - GROUP");
        submitter
            .NewThreads.ShouldHaveSingleItem()
            .Title.ShouldBe("Some Movie 2021 1080p BluRay - GROUP");
    }

    [Test]
    public async Task PostAsync_SkippedRelease_CreatesNoNotification()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(
            repository,
            new FakeForumPostSubmitter(),
            notificationService: notificationService
        );

        // Act
        await service.PostAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        notificationService.Created.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateAsync_StoredTemplate_RendersTheStoredTemplateAndEditsThePost()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var renderer = new FakeForumPostContentRenderer();
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter, renderer);

        // Act
        var result = await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([3]);

        var edited = submitter.EditedPosts.ShouldHaveSingleItem();
        edited.RegistrationId.ShouldBe(5);
        edited.PostedUrl.ShouldBe("https://forum.test/threads/1");
        edited.Body.ShouldBe(renderer.Content);
    }

    [Test]
    public async Task UpdateAsync_WithoutStoredTemplate_UsesTheCurrentRuleMatch()
    {
        // Arrange
        var repository = OutdatedRepository();
        var renderer = new FakeForumPostContentRenderer();
        var service = ServiceWith(repository, new FakeForumPostSubmitter(), renderer);

        // Act
        var result = await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([7]);
    }

    [Test]
    public async Task UpdateAsync_TemplateOverride_TakesPrecedenceOverTheStoredTemplate()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var renderer = new FakeForumPostContentRenderer();
        var service = ServiceWith(repository, new FakeForumPostSubmitter(), renderer);

        // Act
        var result = await service.UpdateAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5,
            forumPostTemplateIdOverride: 11
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([11]);
    }

    [Test]
    public async Task UpdateAsync_NoTemplateResolvable_Fails()
    {
        // Arrange
        var repository = OutdatedRepository(
            registration: AutoPostTestFactory.Registration(
                id: 5,
                name: "Board",
                AutoPostTestFactory.Rule(resolutions: ["R2160p"])
            )
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Failed);
        result.Errors.ShouldHaveSingleItem();
        submitter.EditedPosts.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateAsync_SuccessfulEdit_StoresTheNormalizedUrlAndTheTemplate()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter
        {
            EditedPostUrl = "https://forum.test/threads/name.42/post-1234",
        };
        var service = ServiceWith(repository, submitter);

        // Act
        await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        var updated = repository.UpdatedPostedLocations.ShouldHaveSingleItem();
        updated.PostedLocationId.ShouldBe(9);
        updated.Url.ShouldBe("https://forum.test/threads/name.42/post-1234");
        updated.ForumPostTemplateId.ShouldBe(3);
    }

    [Test]
    public async Task UpdateAsync_SuccessfulEdit_CreatesANotification()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter();
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(repository, submitter, notificationService: notificationService);

        // Act
        await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        var notification = notificationService.Created.ShouldHaveSingleItem();
        notification.Kind.ShouldBe(NotificationKind.AutomaticForumPostUpdated);
        notification.Message.ShouldBe(
            $"The post of '{AutoPostTestFactory.ReleaseName}' in 'Board' was updated: {submitter.EditedPostUrl}"
        );
    }

    [Test]
    public async Task UpdateAsync_FailingEdit_CreatesAFailureNotification()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter { FailingRegistrationIds = [5] };
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(repository, submitter, notificationService: notificationService);

        // Act
        var result = await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Failed);
        repository.UpdatedPostedLocations.ShouldBeEmpty();
        notificationService
            .Created.ShouldHaveSingleItem()
            .Kind.ShouldBe(NotificationKind.AutomaticForumPostUpdateFailed);
    }

    [Test]
    public async Task UpdateAsync_SiteWithoutPostedLocation_IsSkipped()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
        };
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostUpdateSkipReason.NotPosted);
        submitter.EditedPosts.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateAsync_BlockedRelease_IsSkipped()
    {
        // Arrange
        var repository = OutdatedRepository(
            forumPostTemplateId: 3,
            release: AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)
        );
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostUpdateSkipReason.Blocked);
        result.BlockedReason.ShouldBe(AutoPostBlockedReason.QualityGateNotPassed);
    }

    [Test]
    public async Task UpdatePostedLocationAsync_CollectionPost_EditsThePostWithTheGivenTemplate()
    {
        // Arrange
        var repository = OutdatedRepository();
        repository.UpdateTargets.Add(
            new AutoPostUpdateTarget(
                PostedLocationId: 9,
                EntityId: 4,
                EntityName: "Some Collection",
                ReleaseId: null,
                DistributionSiteRegistrationId: 5,
                DistributionSiteRegistrationName: "Board",
                PostedUrl: "https://forum.test/threads/1",
                ForumPostTemplateId: null
            )
        );
        var renderer = new FakeForumPostContentRenderer();
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter, renderer);

        // Act
        var result = await service.UpdatePostedLocationAsync(
            postedLocationId: 9,
            forumPostTemplateIdOverride: 12
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([12]);
        submitter
            .EditedPosts.ShouldHaveSingleItem()
            .PostedUrl.ShouldBe("https://forum.test/threads/1");
        repository.UpdatedPostedLocations.ShouldHaveSingleItem().ForumPostTemplateId.ShouldBe(12);
        result.ReleaseMarkedPosted.ShouldBeFalse();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateAsync_LastStaleSite_MarksTheReleasePosted()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.ReleaseMarkedPosted.ShouldBeTrue();
        repository.MarkedReleaseIds.ShouldBe([1]);
    }

    [Test]
    public async Task UpdateAsync_AnotherStaleSiteRemains_DoesNotMarkTheReleasePosted()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        repository.Registrations.Add(AutoPostTestFactory.Registration(id: 6, name: "Other"));
        repository.PostedLocations.Add(
            AutoPostTestFactory.PostedLocation(
                postedLocationId: 10,
                distributionSiteRegistrationId: 6,
                forumPostTemplateId: 3
            )
        );
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.UpdateAsync(releaseId: 1, distributionSiteRegistrationId: 5);

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        result.ReleaseMarkedPosted.ShouldBeFalse();
        repository.MarkedReleaseIds.ShouldBeEmpty();
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

    private static FakeAutoForumPostingRepository OutdatedRepository(
        int? forumPostTemplateId = null,
        Release? release = null,
        AutoPostRegistration? registration = null
    )
    {
        return new FakeAutoForumPostingRepository
        {
            Releases = [release ?? AutoPostTestFactory.Release()],
            Registrations = [registration ?? AutoPostTestFactory.Registration(id: 5)],
            PostedLocations =
            [
                AutoPostTestFactory.PostedLocation(
                    postedLocationId: 9,
                    forumPostTemplateId: forumPostTemplateId
                ),
            ],
            LatestUploadCompletionTimes = { [1] = AutoPostTestFactory.ReuploadedAt },
        };
    }

    private static AutoForumPostingService ServiceWith(
        FakeAutoForumPostingRepository repository,
        FakeForumPostSubmitter submitter,
        FakeForumPostContentRenderer? contentRenderer = null,
        FakeNotificationService? notificationService = null
    )
    {
        return new AutoForumPostingService(
            new AutoForumPostingPlanService(repository),
            repository,
            contentRenderer ?? new FakeForumPostContentRenderer(),
            submitter,
            notificationService ?? new FakeNotificationService()
        );
    }
}
