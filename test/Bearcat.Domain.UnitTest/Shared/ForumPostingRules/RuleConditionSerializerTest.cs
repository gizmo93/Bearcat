using Bearcat.Domain.Shared.ForumPostingRules;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostingRules;

public class RuleConditionSerializerTest
{
    [Test]
    public void Serialize_ComparisonCondition_WritesCamelCaseEnums()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateMultiValueComparison(
            "Resolution",
            RuleConditionOperator.In,
            "R1080p",
            "R720p"
        );

        // Act
        var json = RuleConditionSerializer.Serialize(condition);

        // Assert
        json.ShouldBe(
            """{"kind":"comparison","field":"Resolution","operator":"in","values":["R1080p","R720p"]}"""
        );
    }

    [Test]
    public void SerializeThenDeserialize_NestedTree_RoundTrips()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateAll(
            RuleConditionFactory.CreateNot(
                RuleConditionFactory.CreateComparison(
                    "PrimaryLanguage",
                    RuleConditionOperator.Equals,
                    "German"
                )
            ),
            RuleConditionFactory.CreateAny(
                RuleConditionFactory.CreateMultiValueComparison(
                    "Resolution",
                    RuleConditionOperator.In,
                    "R1080p",
                    "R720p"
                ),
                RuleConditionFactory.CreateComparison(
                    "ReleaseName",
                    RuleConditionOperator.Like,
                    "%REMUX%"
                )
            )
        );

        // Act
        var succeeded = RuleConditionSerializer.TryDeserialize(
            RuleConditionSerializer.Serialize(condition),
            out var roundTripped
        );

        // Assert
        succeeded.ShouldBeTrue();
        roundTripped.ShouldNotBeNull().Kind.ShouldBe(RuleConditionKind.All);
        roundTripped.Children!.Count.ShouldBe(2);
        roundTripped.Children[0].Kind.ShouldBe(RuleConditionKind.Not);
        roundTripped.Children[0].Child!.Field.ShouldBe("PrimaryLanguage");
        roundTripped.Children[0].Child!.Operator.ShouldBe(RuleConditionOperator.Equals);
        roundTripped.Children[0].Child!.Value.ShouldBe("German");
        roundTripped.Children[1].Kind.ShouldBe(RuleConditionKind.Any);
        roundTripped.Children[1].Children![0].Values.ShouldBe(["R1080p", "R720p"]);
        roundTripped.Children[1].Children![1].Operator.ShouldBe(RuleConditionOperator.Like);
    }

    [TestCase("   ")]
    [TestCase("{\"kind\":\"nonsense\"}")]
    public void TryDeserialize_Garbage_ReturnsFalse(string json)
    {
        // Act
        var succeeded = RuleConditionSerializer.TryDeserialize(json, out var condition);

        // Assert
        succeeded.ShouldBeFalse();
        condition.ShouldBeNull();
    }
}
