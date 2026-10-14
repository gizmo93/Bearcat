using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.PostUpdate;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums.PostUpdate;

public class AutoForumPostUpdateServiceTest
{
    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_StoredTemplate_RendersTheStoredTemplateAndEditsThePost()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var renderer = new FakeForumPostContentRenderer();
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter, renderer);

        // Act
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([3]);

        var edited = submitter.EditedPosts.ShouldHaveSingleItem();
        edited.RegistrationId.ShouldBe(5);
        edited.PostedUrl.ShouldBe("https://forum.test/threads/1");
        edited.Body.ShouldBe(renderer.Content);
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_WithoutStoredTemplate_UsesTheCurrentRuleMatch()
    {
        // Arrange
        var repository = OutdatedRepository();
        var renderer = new FakeForumPostContentRenderer();
        var service = ServiceWith(repository, new FakeForumPostSubmitter(), renderer);

        // Act
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([7]);
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_TemplateOverride_TakesPrecedenceOverTheStoredTemplate()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var renderer = new FakeForumPostContentRenderer();
        var service = ServiceWith(repository, new FakeForumPostSubmitter(), renderer);

        // Act
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5,
            forumPostTemplateIdOverride: 11
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([11]);
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_NoTemplateResolvable_Fails()
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
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Failed);
        result.Errors.ShouldHaveSingleItem();
        submitter.EditedPosts.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_SuccessfulEdit_StoresTheNormalizedUrlAndTheTemplate()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter
        {
            EditedPostUrl = "https://forum.test/threads/name.42/post-1234",
        };
        var service = ServiceWith(repository, submitter);

        // Act
        await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        var updated = repository.UpdatedPostedLocations.ShouldHaveSingleItem();
        updated.PostedLocationId.ShouldBe(9);
        updated.Url.ShouldBe("https://forum.test/threads/name.42/post-1234");
        updated.ForumPostTemplateId.ShouldBe(3);
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_SuccessfulEdit_CreatesANotification()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter();
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(repository, submitter, notificationService: notificationService);

        // Act
        await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        var notification = notificationService.Created.ShouldHaveSingleItem();
        notification.Kind.ShouldBe(NotificationKind.AutomaticForumPostUpdated);
        notification.Message.ShouldBe(
            $"The post of '{AutoPostTestFactory.ReleaseName}' in 'Board' was updated: {submitter.EditedPostUrl}"
        );
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_FailingEdit_CreatesAFailureNotification()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter { FailingRegistrationIds = [5] };
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(repository, submitter, notificationService: notificationService);

        // Act
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Failed);
        repository.UpdatedPostedLocations.ShouldBeEmpty();
        notificationService
            .Created.ShouldHaveSingleItem()
            .Kind.ShouldBe(NotificationKind.AutomaticForumPostUpdateFailed);
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_SiteWithoutPostedLocation_IsSkipped()
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
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostUpdateSkipReason.NotPosted);
        submitter.EditedPosts.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_BlockedRelease_IsSkipped()
    {
        // Arrange
        var repository = OutdatedRepository(
            forumPostTemplateId: 3,
            release: AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)
        );
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

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
    public async Task UpdatePostOfReleaseOnSiteAsync_LastStaleSite_MarksTheReleasePosted()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.ReleaseMarkedPosted.ShouldBeTrue();
        repository.MarkedReleaseIds.ShouldBe([1]);
    }

    [Test]
    public async Task UpdatePostOfReleaseOnSiteAsync_AnotherStaleSiteRemains_DoesNotMarkTheReleasePosted()
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
        var result = await service.UpdatePostOfReleaseOnSiteAsync(
            releaseId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        result.ReleaseMarkedPosted.ShouldBeFalse();
        repository.MarkedReleaseIds.ShouldBeEmpty();
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

    private static AutoForumPostUpdateService ServiceWith(
        FakeAutoForumPostingRepository repository,
        FakeForumPostSubmitter submitter,
        FakeForumPostContentRenderer? contentRenderer = null,
        FakeNotificationService? notificationService = null
    )
    {
        var planService = new AutoForumPostingPlanService(repository);

        return new AutoForumPostUpdateService(
            planService,
            repository,
            contentRenderer ?? new FakeForumPostContentRenderer(),
            submitter,
            notificationService ?? new FakeNotificationService(),
            new ReleasePostedStatusService(planService, repository)
        );
    }
}
