using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostingRules;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageForumPostingRules;

public class ConditionModelMapperTest
{
    [Test]
    public void ToCondition_NestedTree_KeepsStructure()
    {
        // Arrange
        var root = new ConditionNodeModel
        {
            Kind = RuleConditionKind.All,
            Children =
            [
                Comparison("Resolution", RuleConditionOperator.Equals, "R1080p"),
                new ConditionNodeModel
                {
                    Kind = RuleConditionKind.Any,
                    Children =
                    [
                        Comparison("ContentType", RuleConditionOperator.Equals, "Movie"),
                        new ConditionNodeModel
                        {
                            Kind = RuleConditionKind.Not,
                            Children =
                            [
                                Comparison(
                                    "PrimaryLanguage",
                                    RuleConditionOperator.IsNotSet,
                                    value: null
                                ),
                            ],
                        },
                    ],
                },
            ],
        };

        // Act
        var condition = ConditionModelMapper.ToCondition(root);

        // Assert
        condition.Kind.ShouldBe(RuleConditionKind.All);
        condition.Children.ShouldNotBeNull().Count.ShouldBe(2);

        var any = condition.Children[1];
        any.Kind.ShouldBe(RuleConditionKind.Any);

        var not = any.Children.ShouldNotBeNull()[1];
        not.Kind.ShouldBe(RuleConditionKind.Not);
        not.Children.ShouldBeNull();
        not.Child.ShouldNotBeNull().Field.ShouldBe("PrimaryLanguage");
    }

    [Test]
    public void ToCondition_OperatorWithoutValue_OmitsValueAndValues()
    {
        // Arrange
        var model = Comparison("PrimaryLanguage", RuleConditionOperator.IsSet, value: "ignored");
        model.Values = ["ignored"];

        // Act
        var condition = ConditionModelMapper.ToCondition(model);

        // Assert
        condition.Value.ShouldBeNull();
        condition.Values.ShouldBeNull();
    }

    [Test]
    public void ToCondition_MultiValueOperator_WritesValuesOnly()
    {
        // Arrange
        var model = Comparison("Resolution", RuleConditionOperator.In, value: null);
        model.Values = ["R1080p", "R720p"];

        // Act
        var condition = ConditionModelMapper.ToCondition(model);

        // Assert
        condition.Value.ShouldBeNull();
        condition.Values.ShouldBe(["R1080p", "R720p"]);
    }

    [Test]
    public void ToJson_ValidTree_PassesDomainValidation()
    {
        // Arrange
        var root = ConditionModelMapper.CreateDefaultRoot();
        root.Children[0] = Comparison("Resolution", RuleConditionOperator.Equals, "R1080p");

        // Act
        var json = ConditionModelMapper.ToJson(root);

        // Assert
        RuleConditionValidator.ValidateJson(json, RuleFieldCatalog.Fields).ShouldBeEmpty();
    }

    [Test]
    public void ToModel_RoundTrip_KeepsFieldsOperatorsAndValues()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Kind = RuleConditionKind.All,
            Children =
            [
                new RuleCondition
                {
                    Kind = RuleConditionKind.Comparison,
                    Field = "Resolution",
                    Operator = RuleConditionOperator.In,
                    Values = ["R1080p", "R2160p"],
                },
                new RuleCondition
                {
                    Kind = RuleConditionKind.Not,
                    Child = new RuleCondition
                    {
                        Kind = RuleConditionKind.Comparison,
                        Field = "ReleaseGroupName",
                        Operator = RuleConditionOperator.Like,
                        Value = "%Test%",
                    },
                },
            ],
        };

        // Act
        var roundTripped = ConditionModelMapper.ToCondition(
            ConditionModelMapper.ToModel(condition)
        );

        // Assert
        RuleConditionSerializer
            .Serialize(roundTripped)
            .ShouldBe(RuleConditionSerializer.Serialize(condition));
    }

    [Test]
    public void FromJson_InvalidJson_ReturnsDefaultRoot()
    {
        // Act
        var model = ConditionModelMapper.FromJson("{ broken");

        // Assert
        model.Kind.ShouldBe(RuleConditionKind.All);
        model.Children.ShouldHaveSingleItem().Kind.ShouldBe(RuleConditionKind.Comparison);
    }

    [Test]
    public void ToModel_EmptyGroup_GetsOneComparisonChild()
    {
        // Arrange
        var condition = new RuleCondition { Kind = RuleConditionKind.All, Children = [] };

        // Act
        var model = ConditionModelMapper.ToModel(condition);

        // Assert
        model.Children.ShouldHaveSingleItem().Kind.ShouldBe(RuleConditionKind.Comparison);
    }

    [Test]
    public void ApplyField_OperatorNotAllowedForNewField_FallsBackToFirstAllowedOperator()
    {
        // Arrange
        var model = Comparison("ReleaseName", RuleConditionOperator.Regex, "^Something");

        // Act
        ConditionModelMapper.ApplyField(model, "Year");

        // Assert
        model.Field.ShouldBe("Year");
        model.Operator.ShouldBe(RuleConditionOperator.Equals);
        model.Value.ShouldBeEmpty();
        model.Values.ShouldBeEmpty();
    }

    [Test]
    public void ApplyOperator_SwitchingToMultiValue_ClearsSingleValue()
    {
        // Arrange
        var model = Comparison("Resolution", RuleConditionOperator.Equals, "R1080p");

        // Act
        ConditionModelMapper.ApplyOperator(model, RuleConditionOperator.In);

        // Assert
        model.Value.ShouldBeEmpty();
        model.Values.ShouldBeEmpty();
    }

    [Test]
    public void ApplyOperator_StayingSingleValue_KeepsValue()
    {
        // Arrange
        var model = Comparison("Resolution", RuleConditionOperator.Equals, "R1080p");

        // Act
        ConditionModelMapper.ApplyOperator(model, RuleConditionOperator.NotEquals);

        // Assert
        model.Value.ShouldBe("R1080p");
    }

    [Test]
    public void RemoveComparisonsWithUnsupportedFields_NestedUnsupportedField_RemovesOnlyThoseComparisons()
    {
        // Arrange
        var root = new ConditionNodeModel
        {
            Kind = RuleConditionKind.All,
            Children =
            [
                Comparison("Episode", RuleConditionOperator.Equals, "1"),
                Comparison("Season", RuleConditionOperator.Equals, "1"),
                new ConditionNodeModel
                {
                    Kind = RuleConditionKind.Not,
                    Children = [Comparison("Episode", RuleConditionOperator.IsSet, value: null)],
                },
            ],
        };

        // Act
        ConditionModelMapper.RemoveComparisonsWithUnsupportedFields(
            root,
            RuleFieldCatalog.GetFields(ForumPostTemplateType.ReleaseCollection)
        );

        // Assert
        root.Children.Count.ShouldBe(2);
        root.Children[0].Field.ShouldBe("Season");
        root.Children[1].Kind.ShouldBe(RuleConditionKind.Not);
        root.Children[1].Children.ShouldBeEmpty();
    }

    private static ConditionNodeModel Comparison(
        string fieldName,
        RuleConditionOperator conditionOperator,
        string? value
    )
    {
        return new ConditionNodeModel
        {
            Kind = RuleConditionKind.Comparison,
            Field = fieldName,
            Operator = conditionOperator,
            Value = value ?? string.Empty,
        };
    }
}
