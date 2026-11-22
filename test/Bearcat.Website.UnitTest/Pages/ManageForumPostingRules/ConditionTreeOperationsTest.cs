using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostingRules;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageForumPostingRules;

public class ConditionTreeOperationsTest
{
    [Test]
    public void ChangeField_OperatorNotAllowedForNewField_FallsBackToFirstAllowedOperator()
    {
        // Arrange
        var comparison = CreateComparison("ReleaseName", RuleConditionOperator.Regex, "^Something");

        // Act
        ConditionTreeOperations.ChangeField(comparison, "Year");

        // Assert
        comparison.Field.ShouldBe("Year");
        comparison.Operator.ShouldBe(RuleConditionOperator.Equals);
        comparison.Value.ShouldBeEmpty();
    }

    [TestCase(RuleConditionOperator.In, "")]
    [TestCase(RuleConditionOperator.NotEquals, "R1080p")]
    public void ChangeOperator_KeepsValueOnlyWhenValueCountStaysTheSame(
        RuleConditionOperator newOperator,
        string expectedValue
    )
    {
        // Arrange
        var comparison = CreateComparison("Resolution", RuleConditionOperator.Equals, "R1080p");

        // Act
        ConditionTreeOperations.ChangeOperator(comparison, newOperator);

        // Assert
        comparison.Value.ShouldBe(expectedValue);
    }

    [Test]
    public void RemoveComparisonsWithUnsupportedFields_NestedUnsupportedField_RemovesOnlyThoseComparisons()
    {
        // Arrange
        var root = new RuleCondition
        {
            Kind = RuleConditionKind.All,
            Children =
            [
                CreateComparison("Episode", RuleConditionOperator.Equals, "1"),
                CreateComparison("Season", RuleConditionOperator.Equals, "1"),
                new RuleCondition
                {
                    Kind = RuleConditionKind.Not,
                    Child = CreateComparison("Episode", RuleConditionOperator.IsSet, string.Empty),
                },
            ],
        };

        // Act
        ConditionTreeOperations.RemoveComparisonsWithUnsupportedFields(
            root,
            RuleFieldCatalog.GetFields(ForumPostTemplateType.ReleaseCollection)
        );

        // Assert
        root.Children.Count.ShouldBe(2);
        root.Children[0].Field.ShouldBe("Season");
        root.Children[1].Kind.ShouldBe(RuleConditionKind.Not);
        root.Children[1].Child.ShouldBeNull();
    }

    private static RuleCondition CreateComparison(
        string fieldName,
        RuleConditionOperator conditionOperator,
        string value
    )
    {
        return new RuleCondition
        {
            Kind = RuleConditionKind.Comparison,
            Field = fieldName,
            Operator = conditionOperator,
            Value = value,
        };
    }
}
