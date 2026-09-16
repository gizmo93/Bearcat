using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public class AutoForumPostingPlanServiceTest
{
    [Test]
    public async Task GetPlanAsync_QualityGateNotPassed_IsBlockedWithoutSites()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(qualityGateState: QualityGateState.Failed)],
            Registrations = [AutoPostTestFactory.Registration()],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.BlockedReason.ShouldBe(AutoPostBlockedReason.QualityGateNotPassed);
        plan.Sites.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPlanAsync_ManuallyApprovedQualityGate_IsNotBlocked()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases =
            [
                AutoPostTestFactory.Release(qualityGateState: QualityGateState.ManuallyApproved),
            ],
            Registrations = [AutoPostTestFactory.Registration()],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.IsBlocked.ShouldBeFalse();
        plan.Sites.ShouldHaveSingleItem().Status.ShouldBe(AutoPostSiteStatus.Matched);
    }

    [Test]
    public async Task GetPlanAsync_MissingClassification_IsBlocked()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(withClassification: false)],
            Registrations = [AutoPostTestFactory.Registration()],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.BlockedReason.ShouldBe(AutoPostBlockedReason.ClassificationMissing);
        plan.Sites.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPlanAsync_PostedLocationForRegistration_ReportsAlreadyPosted()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            PostedLocations = [AutoPostTestFactory.PostedLocation()],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        var site = plan.Sites.ShouldHaveSingleItem();
        site.Status.ShouldBe(AutoPostSiteStatus.AlreadyPosted);
        site.PostedUrl.ShouldBe("https://forum.test/threads/1");
        plan.PendingSites.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPlanAsync_AlreadyPosted_KeepsTheMatchingRuleAndTheStoredTemplate()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            PostedLocations =
            [
                AutoPostTestFactory.PostedLocation(postedLocationId: 9, forumPostTemplateId: 3),
            ],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        var site = plan.Sites.ShouldHaveSingleItem();
        site.Status.ShouldBe(AutoPostSiteStatus.AlreadyPosted);
        site.PostedLocationId.ShouldBe(9);
        site.StoredForumPostTemplateId.ShouldBe(3);
        site.Match.ShouldNotBeNull().ForumPostTemplateId.ShouldBe(7);
        site.ResolvedForumPostTemplateId.ShouldBe(3);
    }

    [Test]
    public async Task GetPlanAsync_AlreadyPostedWithoutStoredTemplate_FallsBackToTheRuleTemplate()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            PostedLocations = [AutoPostTestFactory.PostedLocation()],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.Sites.ShouldHaveSingleItem().ResolvedForumPostTemplateId.ShouldBe(7);
    }

    [Test]
    public async Task GetPlanAsync_UploadNewerThanThePost_NeedsContentUpdate()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            PostedLocations = [AutoPostTestFactory.PostedLocation()],
            LatestUploadCompletionTimes = { [1] = AutoPostTestFactory.ReuploadedAt },
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.Sites.ShouldHaveSingleItem().NeedsContentUpdate.ShouldBeTrue();
        plan.UpdatableSites.ShouldHaveSingleItem().DistributionSiteRegistrationId.ShouldBe(5);
    }

    [Test]
    public async Task GetPlanAsync_ContentUpdatedAfterTheUpload_NeedsNoContentUpdate()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            PostedLocations =
            [
                AutoPostTestFactory.PostedLocation(
                    contentUpdatedAt: AutoPostTestFactory.ReuploadedAt.AddMinutes(5)
                ),
            ],
            LatestUploadCompletionTimes = { [1] = AutoPostTestFactory.ReuploadedAt },
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.Sites.ShouldHaveSingleItem().NeedsContentUpdate.ShouldBeFalse();
        plan.UpdatableSites.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPlanAsync_ReleaseWithoutCompletedUploads_NeedsNoContentUpdate()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            PostedLocations = [AutoPostTestFactory.PostedLocation()],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.Sites.ShouldHaveSingleItem().NeedsContentUpdate.ShouldBeFalse();
        plan.UpdatableSites.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPlanAsync_ManualRegistration_HasNoAutomaticallyUpdatableSites()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            PostedLocations = [AutoPostTestFactory.PostedLocation()],
            LatestUploadCompletionTimes = { [1] = AutoPostTestFactory.ReuploadedAt },
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.UpdatableSites.ShouldHaveSingleItem();
        plan.AutomaticallyUpdatableSites.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPlanAsync_PostedLocationWithoutRegistration_IsIgnored()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations = [AutoPostTestFactory.Registration(id: 5)],
            PostedLocations =
            [
                AutoPostTestFactory.PostedLocation(
                    distributionSiteRegistrationId: null,
                    url: "https://manual.test/1"
                ),
            ],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.Sites.ShouldHaveSingleItem().Status.ShouldBe(AutoPostSiteStatus.Matched);
    }

    [Test]
    public async Task GetPlanAsync_MatchingRule_ReportsRuleAndTarget()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations =
            [
                AutoPostTestFactory.Registration(
                    id: 5,
                    name: "Board",
                    AutoPostTestFactory.Rule(
                        id: 3,
                        threadPrefixId: "11",
                        postMode: ForumPostPostMode.ReplyToExistingElseNewThread
                    )
                ),
            ],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        var site = plan.Sites.ShouldHaveSingleItem();
        site.DistributionSiteRegistrationName.ShouldBe("Board");
        site.Status.ShouldBe(AutoPostSiteStatus.Matched);

        var match = site.Match.ShouldNotBeNull();
        match.ForumPostingRuleId.ShouldBe(3);
        match.RuleName.ShouldBe("Rule 3");
        match.TargetNodeId.ShouldBe("42");
        match.TargetPathSnapshot.ShouldBe("Board › Movies › HD");
        match.ThreadPrefixId.ShouldBe("11");
        match.ForumPostTemplateId.ShouldBe(7);
        match.PostMode.ShouldBe(ForumPostPostMode.ReplyToExistingElseNewThread);
    }

    [Test]
    public async Task GetPlanAsync_NoRuleMatches_ReportsNoMatch()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release(resolution: ReleaseResolution.R720p)],
            Registrations = [AutoPostTestFactory.Registration()],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        var site = plan.Sites.ShouldHaveSingleItem();
        site.Status.ShouldBe(AutoPostSiteStatus.NoMatch);
        site.Match.ShouldBeNull();
    }

    [Test]
    public async Task GetPlanAsync_SeveralMatchingRules_UsesLowestSortOrder()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases = [AutoPostTestFactory.Release()],
            Registrations =
            [
                AutoPostTestFactory.Registration(
                    id: 5,
                    name: "Board",
                    AutoPostTestFactory.Rule(id: 1, sortOrder: 2),
                    AutoPostTestFactory.Rule(id: 2, sortOrder: 1)
                ),
            ],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plan = await service.GetPlanAsync(releaseId: 1);

        // Assert
        plan.Sites.ShouldHaveSingleItem().Match.ShouldNotBeNull().ForumPostingRuleId.ShouldBe(2);
    }

    [Test]
    public async Task GetPlansAsync_SeveralReleases_ReturnsOnePlanPerRequestedId()
    {
        // Arrange
        var repository = new FakeAutoForumPostingRepository
        {
            Releases =
            [
                AutoPostTestFactory.Release(id: 1),
                AutoPostTestFactory.Release(id: 2, resolution: ReleaseResolution.R720p),
            ],
            Registrations = [AutoPostTestFactory.Registration()],
        };
        var service = new AutoForumPostingPlanService(repository);

        // Act
        var plans = await service.GetPlansAsync([2, 1]);

        // Assert
        plans.Select(plan => plan.ReleaseId).ShouldBe([2, 1]);
        plans[0].Sites.ShouldHaveSingleItem().Status.ShouldBe(AutoPostSiteStatus.NoMatch);
        plans[1].Sites.ShouldHaveSingleItem().Status.ShouldBe(AutoPostSiteStatus.Matched);
    }
}
