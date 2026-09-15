using Bearcat.Domain.Shared.ForumPostingRules;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostingRules;

public class RuleConditionSerializerTest
{
    [Test]
    public void Serialize_ComparisonCondition_WritesCamelCaseEnums()
    {
        // Arrange
        var condition = RuleCondition.CompareMany(
            RuleFieldCatalog.Resolution,
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
        var condition = RuleCondition.All(
            RuleCondition.Not(
                RuleCondition.Compare(
                    RuleFieldCatalog.PrimaryLanguage,
                    RuleConditionOperator.Equals,
                    "German"
                )
            ),
            RuleCondition.Any(
                RuleCondition.CompareMany(
                    RuleFieldCatalog.Resolution,
                    RuleConditionOperator.In,
                    "R1080p",
                    "R720p"
                ),
                RuleCondition.Compare(
                    RuleFieldCatalog.ReleaseName,
                    RuleConditionOperator.Like,
                    "%REMUX%"
                )
            )
        );

        // Act
        var roundTripped = RuleConditionSerializer.Deserialize(
            RuleConditionSerializer.Serialize(condition)
        );

        // Assert
        roundTripped.Kind.ShouldBe(RuleConditionKind.All);
        roundTripped.Children!.Count.ShouldBe(2);
        roundTripped.Children[0].Kind.ShouldBe(RuleConditionKind.Not);
        roundTripped.Children[0].Child!.Field.ShouldBe(RuleFieldCatalog.PrimaryLanguage);
        roundTripped.Children[0].Child!.Operator.ShouldBe(RuleConditionOperator.Equals);
        roundTripped.Children[0].Child!.Value.ShouldBe("German");
        roundTripped.Children[1].Kind.ShouldBe(RuleConditionKind.Any);
        roundTripped.Children[1].Children![0].Values.ShouldBe(["R1080p", "R720p"]);
        roundTripped.Children[1].Children![1].Operator.ShouldBe(RuleConditionOperator.Like);
    }

    [Test]
    public void Serialize_GroupCondition_OmitsComparisonMembers()
    {
        // Arrange
        var condition = RuleCondition.Not(
            RuleCondition.Compare(
                RuleFieldCatalog.Year,
                RuleConditionOperator.GreaterOrEqual,
                "2020"
            )
        );

        // Act
        var json = RuleConditionSerializer.Serialize(condition);

        // Assert
        json.ShouldBe(
            """{"kind":"not","child":{"kind":"comparison","field":"Year","operator":"greaterOrEqual","value":"2020"}}"""
        );
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not json at all")]
    [TestCase("{\"kind\":")]
    [TestCase("{\"kind\":\"nonsense\"}")]
    public void TryDeserialize_Garbage_ReturnsFalse(string json)
    {
        // Act
        var succeeded = RuleConditionSerializer.TryDeserialize(json, out var condition);

        // Assert
        succeeded.ShouldBeFalse();
        condition.ShouldBeNull();
    }

    [Test]
    public void Deserialize_Garbage_ThrowsRuleConditionFormatException()
    {
        // Act + Assert
        Should.Throw<RuleConditionFormatException>(() =>
            RuleConditionSerializer.Deserialize("{ broken")
        );
    }

    [Test]
    public void Validate_ValidCondition_ReturnsNoErrors()
    {
        // Arrange
        var condition = RuleCondition.All(
            RuleCondition.Compare(
                RuleFieldCatalog.ContentType,
                RuleConditionOperator.Equals,
                "Movie"
            ),
            RuleCondition.Compare(
                RuleFieldCatalog.PrimaryLanguage,
                RuleConditionOperator.IsSet,
                null
            )
        );

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Test]
    public void Validate_UnknownField_ReturnsError()
    {
        // Arrange
        var condition = RuleCondition.Compare("Nonsense", RuleConditionOperator.Equals, "x");

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("Unknown field 'Nonsense'");
    }

    [Test]
    public void Validate_OperatorNotAllowedForField_ReturnsError()
    {
        // Arrange
        var condition = RuleCondition.Compare(
            RuleFieldCatalog.IsMultiLanguage,
            RuleConditionOperator.Regex,
            "true"
        );

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors
            .ShouldHaveSingleItem()
            .ShouldContain("is not allowed for the field 'IsMultiLanguage'");
    }

    [Test]
    public void Validate_MissingValue_ReturnsError()
    {
        // Arrange
        var condition = RuleCondition.Compare(
            RuleFieldCatalog.ReleaseName,
            RuleConditionOperator.Like,
            null
        );

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("requires a value");
    }

    [Test]
    public void Validate_InOperatorWithoutValues_ReturnsError()
    {
        // Arrange
        var condition = RuleCondition.CompareMany(
            RuleFieldCatalog.Resolution,
            RuleConditionOperator.In
        );

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("requires at least one value");
    }

    [Test]
    public void Validate_EmptyGroup_ReturnsError()
    {
        // Arrange
        var condition = RuleCondition.All();

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("requires at least one child condition");
    }

    [Test]
    public void Validate_NotWithoutChild_ReturnsError()
    {
        // Arrange
        var condition = new RuleCondition { Kind = RuleConditionKind.Not };

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("requires a child condition");
    }

    [Test]
    public void Validate_ComparisonWithChildren_ReturnsError()
    {
        // Arrange
        var condition = RuleCondition.Compare(
            RuleFieldCatalog.Year,
            RuleConditionOperator.Equals,
            "2020"
        );
        condition.Children =
        [
            RuleCondition.Compare(RuleFieldCatalog.Year, RuleConditionOperator.Equals, "2021"),
        ];

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("must not contain child conditions");
    }

    [Test]
    public void Validate_InvalidEnumerationValue_ReturnsError()
    {
        // Arrange
        var condition = RuleCondition.CompareMany(
            RuleFieldCatalog.Source,
            RuleConditionOperator.In,
            "BluRay",
            "Betamax"
        );

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("The value 'Betamax' is not valid");
    }

    [Test]
    public void Validate_NonNumericIntegerValue_ReturnsError()
    {
        // Arrange
        var condition = RuleCondition.Compare(
            RuleFieldCatalog.Year,
            RuleConditionOperator.GreaterOrEqual,
            "twenty"
        );

        // Act
        var errors = RuleConditionSerializer.Validate(condition, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("is not a number");
    }

    [Test]
    public void Validate_NullCondition_ReturnsError()
    {
        // Act
        var errors = RuleConditionSerializer.Validate(null, RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("A rule condition is required.");
    }

    [Test]
    public void ValidateJson_MalformedJson_ReturnsError()
    {
        // Act
        var errors = RuleConditionSerializer.ValidateJson("{ broken", RuleFieldCatalog.Fields);

        // Assert
        errors.ShouldHaveSingleItem().ShouldContain("not valid JSON");
    }
}
