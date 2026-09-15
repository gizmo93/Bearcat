using Bearcat.Domain.Shared.ForumPostingRules;
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
                Comparison(RuleFieldCatalog.Resolution, RuleConditionOperator.Equals, "R1080p"),
                new ConditionNodeModel
                {
                    Kind = RuleConditionKind.Any,
                    Children =
                    [
                        Comparison(
                            RuleFieldCatalog.ContentType,
                            RuleConditionOperator.Equals,
                            "Movie"
                        ),
                        new ConditionNodeModel
                        {
                            Kind = RuleConditionKind.Not,
                            Children =
                            [
                                Comparison(
                                    RuleFieldCatalog.PrimaryLanguage,
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
        not.Child.ShouldNotBeNull().Field.ShouldBe(RuleFieldCatalog.PrimaryLanguage);
    }

    [Test]
    public void ToCondition_OperatorWithoutValue_OmitsValueAndValues()
    {
        // Arrange
        var model = Comparison(
            RuleFieldCatalog.PrimaryLanguage,
            RuleConditionOperator.IsSet,
            value: "ignored"
        );
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
        var model = Comparison(RuleFieldCatalog.Resolution, RuleConditionOperator.In, value: null);
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
        root.Children[0] = Comparison(
            RuleFieldCatalog.Resolution,
            RuleConditionOperator.Equals,
            "R1080p"
        );

        // Act
        var json = ConditionModelMapper.ToJson(root);

        // Assert
        RuleConditionSerializer.ValidateJson(json, RuleFieldCatalog.Fields).ShouldBeEmpty();
    }

    [Test]
    public void ToModel_RoundTrip_KeepsFieldsOperatorsAndValues()
    {
        // Arrange
        var condition = RuleCondition.All(
            RuleCondition.CompareMany(
                RuleFieldCatalog.Resolution,
                RuleConditionOperator.In,
                "R1080p",
                "R2160p"
            ),
            RuleCondition.Not(
                RuleCondition.Compare(
                    RuleFieldCatalog.ReleaseGroupName,
                    RuleConditionOperator.Like,
                    "%Test%"
                )
            )
        );

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
        // Act
        var model = ConditionModelMapper.ToModel(RuleCondition.All());

        // Assert
        model.Children.ShouldHaveSingleItem().Kind.ShouldBe(RuleConditionKind.Comparison);
    }

    [Test]
    public void ApplyField_OperatorNotAllowedForNewField_FallsBackToFirstAllowedOperator()
    {
        // Arrange
        var model = Comparison(
            RuleFieldCatalog.ReleaseName,
            RuleConditionOperator.Regex,
            "^Something"
        );

        // Act
        ConditionModelMapper.ApplyField(model, RuleFieldCatalog.Year);

        // Assert
        model.Field.ShouldBe(RuleFieldCatalog.Year);
        model.Operator.ShouldBe(RuleConditionOperator.Equals);
        model.Value.ShouldBeEmpty();
        model.Values.ShouldBeEmpty();
    }

    [Test]
    public void ApplyOperator_SwitchingToMultiValue_ClearsSingleValue()
    {
        // Arrange
        var model = Comparison(RuleFieldCatalog.Resolution, RuleConditionOperator.Equals, "R1080p");

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
        var model = Comparison(RuleFieldCatalog.Resolution, RuleConditionOperator.Equals, "R1080p");

        // Act
        ConditionModelMapper.ApplyOperator(model, RuleConditionOperator.NotEquals);

        // Assert
        model.Value.ShouldBe("R1080p");
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
