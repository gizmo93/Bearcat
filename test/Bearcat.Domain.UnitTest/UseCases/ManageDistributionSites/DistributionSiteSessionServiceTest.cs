using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageDistributionSites;

public class DistributionSiteSessionServiceTest
{
    [Test]
    public async Task GetTargetHierarchyAsync_CachedSession_UsesConfiguredSiteWithoutLoggingIn()
    {
        // Arrange
        var registration = new DistributionSiteRegistration
        {
            Id = 12,
            Name = "My forum",
            DistributionSiteClassName = "XenForo",
            SerializedConfig = "encrypted-config",
        };
        var session = new DistributionSession("Bearcat", []);
        var tree = new List<ForumTargetNode>
        {
            new(
                Id: new ForumTargetId("https://example.org/community/forums/9/"),
                Title: "Movies",
                CanReceivePosts: true,
                Children: []
            ),
        };

        var repository = new Mock<IDistributionSiteRegistrationWriteRepository>(
            MockBehavior.Strict
        );
        repository
            .Setup(repo => repo.GetByIdAsync(12, CancellationToken.None))
            .ReturnsAsync(registration);

        var sessionStore = new Mock<IDistributionSessionStore>(MockBehavior.Strict);
        sessionStore
            .Setup(store => store.TryGetAsync(12, CancellationToken.None))
            .ReturnsAsync(session);

        var protector = new Mock<ISecretProtector>(MockBehavior.Strict);
        protector.Setup(secret => secret.Unprotect("encrypted-config")).Returns("decrypted-config");

        var forum = new Mock<IForumDistributionSite>(MockBehavior.Strict);
        forum
            .Setup(site => site.IsSessionValidAsync(session, CancellationToken.None))
            .ReturnsAsync(true);
        forum
            .Setup(site => site.GetTargetHierarchyAsync(session, CancellationToken.None))
            .ReturnsAsync(tree);

        var factory = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);
        factory.Setup(sites => sites.Create("XenForo", "decrypted-config")).Returns(forum.Object);

        var service = new DistributionSiteSessionService(
            repository: repository.Object,
            sessionStore: sessionStore.Object,
            distributionSiteFactory: factory.Object,
            secretProtector: protector.Object
        );

        // Act
        var result = await service.GetTargetHierarchyAsync(12);

        // Assert
        result.ShouldBe(tree);
        factory.Verify(sites => sites.Create("XenForo", "decrypted-config"), Times.Once);
        forum.Verify(
            site =>
                site.LogInAsync(It.IsAny<IDistributionSiteConfig>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task TestLoginAsync_Registration_LogsInWithConfiguredSiteAndStoresSession()
    {
        // Arrange
        var registration = new DistributionSiteRegistration
        {
            Id = 12,
            Name = "My forum",
            DistributionSiteClassName = "XenForo",
            SerializedConfig = "encrypted-config",
        };
        var config = Mock.Of<IDistributionSiteConfig>();
        var session = new DistributionSession("Bearcat", []);
        var repository = new Mock<IDistributionSiteRegistrationWriteRepository>(
            MockBehavior.Strict
        );
        repository
            .Setup(repo => repo.GetByIdAsync(12, CancellationToken.None))
            .ReturnsAsync(registration);

        var protector = new Mock<ISecretProtector>(MockBehavior.Strict);
        protector.Setup(secret => secret.Unprotect("encrypted-config")).Returns("decrypted-config");

        var forum = new Mock<IForumDistributionSite>(MockBehavior.Strict);
        forum.Setup(site => site.DeserializeConfig("decrypted-config")).Returns(config);
        forum.Setup(site => site.LogInAsync(config, CancellationToken.None)).ReturnsAsync(session);

        var factory = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);
        factory.Setup(sites => sites.Create("XenForo", "decrypted-config")).Returns(forum.Object);

        var sessionStore = new Mock<IDistributionSessionStore>(MockBehavior.Strict);
        sessionStore
            .Setup(store => store.SaveAsync(12, session, CancellationToken.None))
            .Returns(Task.CompletedTask);

        var service = new DistributionSiteSessionService(
            repository: repository.Object,
            sessionStore: sessionStore.Object,
            distributionSiteFactory: factory.Object,
            secretProtector: protector.Object
        );

        // Act
        var result = await service.TestLoginAsync(12);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        sessionStore.Verify(
            store => store.SaveAsync(12, session, CancellationToken.None),
            Times.Once
        );
    }
}
