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
    public async Task UpdatePostOnSiteAsync_StoredTemplate_RendersTheStoredTemplateAndEditsThePost()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var renderer = new FakeForumPostContentRenderer();
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter, renderer);

        // Act
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
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
    public async Task UpdatePostOnSiteAsync_WithoutStoredTemplate_UsesTheCurrentRuleMatch()
    {
        // Arrange
        var repository = OutdatedRepository();
        var renderer = new FakeForumPostContentRenderer();
        var service = ServiceWith(repository, new FakeForumPostSubmitter(), renderer);

        // Act
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([7]);
    }

    [Test]
    public async Task UpdatePostOnSiteAsync_TemplateOverride_TakesPrecedenceOverTheStoredTemplate()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var renderer = new FakeForumPostContentRenderer();
        var service = ServiceWith(repository, new FakeForumPostSubmitter(), renderer);

        // Act
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5,
            forumPostTemplateIdOverride: 11
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([11]);
    }

    [Test]
    public async Task UpdatePostOnSiteAsync_NoTemplateResolvable_Fails()
    {
        // Arrange
        var repository = OutdatedRepository(
            registration: AutoPostTestFactory.Registration(
                id: 5,
                name: "Board",
                rules: AutoPostTestFactory.Rule(resolutions: ["R2160p"])
            )
        );
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter);

        // Act
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Failed);
        result.Errors.ShouldHaveSingleItem();
        submitter.EditedPosts.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdatePostOnSiteAsync_SuccessfulEdit_StoresTheNormalizedUrlAndTheTemplate()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter
        {
            EditedPostUrl = "https://forum.test/threads/name.42/post-1234",
        };
        var service = ServiceWith(repository, submitter);

        // Act
        await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        var updated = repository.UpdatedPostedLocations.ShouldHaveSingleItem();
        updated.PostedLocationId.ShouldBe(9);
        updated.Url.ShouldBe("https://forum.test/threads/name.42/post-1234");
        updated.ForumPostTemplateId.ShouldBe(3);
    }

    [Test]
    public async Task UpdatePostOnSiteAsync_SuccessfulEdit_CreatesANotification()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter();
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(repository, submitter, notificationService: notificationService);

        // Act
        await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
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
    public async Task UpdatePostOnSiteAsync_FailingEdit_CreatesAFailureNotification()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var submitter = new FakeForumPostSubmitter { FailingRegistrationIds = [5] };
        var notificationService = new FakeNotificationService();
        var service = ServiceWith(repository, submitter, notificationService: notificationService);

        // Act
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
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
    public async Task UpdatePostOnSiteAsync_SiteWithoutPostedLocation_IsSkipped()
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
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostUpdateSkipReason.NotPosted);
        submitter.EditedPosts.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdatePostOnSiteAsync_BlockedRelease_IsSkipped()
    {
        // Arrange
        var repository = OutdatedRepository(
            forumPostTemplateId: 3,
            release: AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)
        );
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Skipped);
        result.SkipReason.ShouldBe(AutoPostUpdateSkipReason.Blocked);
        result.BlockedReason.ShouldBe(AutoPostBlockedReason.QualityGateNotPassed);
    }

    [Test]
    public async Task UpdatePostedLocationAsync_CollectionPostWithoutOtherPendingSites_EditsThePostAndMarksTheCollectionPosted()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            ReleaseCollections = [AutoPostTestFactory.ReleaseCollection(id: 4)],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            UpdateTargets =
            [
                new AutoPostUpdateTarget(
                    PostedLocationId: 9,
                    EntityType: ForumPostTemplateType.ReleaseCollection,
                    EntityId: 4,
                    EntityName: AutoPostTestFactory.ReleaseCollectionName,
                    DistributionSiteRegistrationId: 5,
                    DistributionSiteRegistrationName: "Board",
                    PostedUrl: "https://forum.test/threads/1",
                    ForumPostTemplateId: null
                ),
            ],
            ReleaseCollectionPostedLocations =
            [
                AutoPostTestFactory.PostedLocation(postedLocationId: 9, entityId: 4),
            ],
        };
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
        result.UploadsMarkedPosted.ShouldBeTrue();
        repository.MarkedReleaseCollectionIds.ShouldBe([4]);
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdatePostOnSiteAsync_LastStaleSite_MarksTheReleasePosted()
    {
        // Arrange
        var repository = OutdatedRepository(forumPostTemplateId: 3);
        var service = ServiceWith(repository, new FakeForumPostSubmitter());

        // Act
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.UploadsMarkedPosted.ShouldBeTrue();
        repository.MarkedReleaseIds.ShouldBe([1]);
    }

    [Test]
    public async Task UpdatePostOnSiteAsync_AnotherStaleSiteRemains_DoesNotMarkTheReleasePosted()
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
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.Release,
            entityId: 1,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        result.UploadsMarkedPosted.ShouldBeFalse();
        repository.MarkedReleaseIds.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdatePostOnSiteAsync_OutdatedCollectionPost_EditsTheExistingPostAndMarksTheCollectionPosted()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            ReleaseCollections = [AutoPostTestFactory.ReleaseCollection(id: 4)],
            Registrations =
            [
                AutoPostTestFactory.Registration(
                    id: 5,
                    name: "Board",
                    rules: AutoPostTestFactory.Rule(
                        forumPostTemplateId: 8,
                        templateType: ForumPostTemplateType.ReleaseCollection
                    )
                ),
            ],
            ReleaseCollectionPostedLocations =
            [
                AutoPostTestFactory.PostedLocation(postedLocationId: 9, entityId: 4),
            ],
            ReleaseCollectionLatestUploadCompletionTimes =
            {
                [4] = AutoPostTestFactory.ReuploadedAt,
            },
            Now = AutoPostTestFactory.ReuploadedAt.AddHours(1),
        };
        var renderer = new FakeForumPostContentRenderer();
        var submitter = new FakeForumPostSubmitter();
        var service = ServiceWith(repository, submitter, renderer);

        // Act
        var result = await service.UpdatePostOnSiteAsync(
            entityType: ForumPostTemplateType.ReleaseCollection,
            entityId: 4,
            distributionSiteRegistrationId: 5
        );

        // Assert
        result.Status.ShouldBe(AutoPostUpdateStatus.Updated);
        renderer.RenderedTemplateIds.ShouldBe([8]);
        submitter
            .EditedPosts.ShouldHaveSingleItem()
            .PostedUrl.ShouldBe("https://forum.test/threads/1");
        submitter.NewThreads.ShouldBeEmpty();
        submitter.Replies.ShouldBeEmpty();
        repository.UpdatedPostedLocations.ShouldHaveSingleItem().PostedLocationId.ShouldBe(9);
        repository.MarkedReleaseCollectionIds.ShouldBe([4]);
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
            planService: planService,
            repository: repository,
            contentRenderer: contentRenderer ?? new FakeForumPostContentRenderer(),
            submitter: submitter,
            notificationService: notificationService ?? new FakeNotificationService(),
            uploadsPostedStatusService: new UploadsPostedStatusService(planService, repository)
        );
    }
}
