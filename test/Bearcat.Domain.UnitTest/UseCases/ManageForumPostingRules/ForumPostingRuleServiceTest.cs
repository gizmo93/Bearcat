using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Dto;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;
using Bearcat.Domain.ValueObjects;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageForumPostingRules;

public class ForumPostingRuleServiceTest
{
    private const int CollectionTemplateId = 3;

    private Mock<IForumPostingRuleWriteRepository> writeRepositoryMock = null!;
    private ForumPostingRuleService service = null!;

    [SetUp]
    public void Setup()
    {
        writeRepositoryMock = new Mock<IForumPostingRuleWriteRepository>();
        writeRepositoryMock
            .Setup(repository =>
                repository.GetForumPostTemplateTypeAsync(
                    CollectionTemplateId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(ForumPostTemplateType.ReleaseCollection);
        service = new ForumPostingRuleService(writeRepositoryMock.Object);
    }

    [Test]
    public async Task CreateAsync_CollectionTemplateWithSeasonCondition_AddsRule()
    {
        // Arrange
        var input = InputWith(RuleFieldCatalog.Season);

        // Act
        await service.CreateAsync(distributionSiteRegistrationId: 5, input);

        // Assert
        writeRepositoryMock.Verify(
            repository =>
                repository.Add(
                    It.Is<ForumPostingRule>(rule =>
                        rule.ForumPostTemplateId == CollectionTemplateId
                    )
                ),
            Times.Once
        );
    }

    [Test]
    public async Task CreateAsync_CollectionTemplateWithEpisodeCondition_Throws()
    {
        // Arrange
        var input = InputWith(RuleFieldCatalog.Episode);

        // Act
        var exception = await Should.ThrowAsync<ArgumentException>(() =>
            service.CreateAsync(distributionSiteRegistrationId: 5, input)
        );

        // Assert
        exception.Message.ShouldContain(RuleFieldCatalog.Episode);
        writeRepositoryMock.Verify(
            repository => repository.Add(It.IsAny<ForumPostingRule>()),
            Times.Never
        );
    }

    private static ForumPostingRuleInput InputWith(string field)
    {
        return new ForumPostingRuleInput(
            Name: "Seasons",
            ConditionJson: RuleConditionSerializer.Serialize(
                RuleCondition.Compare(field, RuleConditionOperator.Equals, "2")
            ),
            TargetNodeId: "42",
            TargetPathSnapshot: "Board > Series",
            ThreadPrefixId: null,
            ForumPostTemplateId: CollectionTemplateId,
            PostMode: ForumPostPostMode.AlwaysNewThread,
            IsEnabled: true
        );
    }
}
