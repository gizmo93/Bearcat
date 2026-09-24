using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Domain.Shared.ConfigurationFields;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ConfigurationFields;

public class ConfigurationValueNormalizerTest
{
    private static readonly IReadOnlyList<ConfigurationField> Fields =
    [
        new("Host", ConfigurationFieldType.Text, IsRequired: true),
        new("Port", ConfigurationFieldType.Number, IsRequired: true, DefaultValue: 21),
        new("Password", ConfigurationFieldType.Password, IsRequired: true),
        new("Mode", ConfigurationFieldType.Select, IsRequired: true, Options: ["Plain", "Secure"]),
        new("Verify", ConfigurationFieldType.Boolean, IsRequired: false),
        new("Comment", ConfigurationFieldType.Text, IsRequired: false),
    ];

    [Test]
    public void Normalize_FormValues_ConvertsToTypedValues()
    {
        // Arrange
        var submitted = ValidSubmission();

        // Act
        var result = ConfigurationValueNormalizer.Normalize(Fields, submitted);

        // Assert
        result["Host"].ShouldBe("ftp.example.org");
        result["Port"].ShouldBeOfType<int>().ShouldBe(2121);
        result["Password"].ShouldBe(" secret ");
        result["Mode"].ShouldBe("Secure");
        result["Verify"].ShouldBeOfType<bool>().ShouldBeTrue();
        result.ShouldNotContainKey("Comment");
    }

    [Test]
    public void Normalize_BooleanAsString_ParsesBoolean()
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted["Verify"] = "False";

        // Act
        var result = ConfigurationValueNormalizer.Normalize(Fields, submitted);

        // Assert
        result["Verify"].ShouldBeOfType<bool>().ShouldBeFalse();
    }

    [Test]
    public void Normalize_UnknownKey_Throws()
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted["Unknown"] = "value";

        // Act
        var exception = Should.Throw<ConfigurationFieldValidationException>(() =>
            ConfigurationValueNormalizer.Normalize(Fields, submitted)
        );

        // Assert
        exception.FieldKey.ShouldBe("Unknown");
        exception.Error.ShouldBe(ConfigurationFieldValidationError.UnknownField);
    }

    [TestCase("Host", "   ")]
    [TestCase("Password", "")]
    [TestCase("Mode", null)]
    public void Normalize_RequiredValueMissingOnCreate_Throws(string key, string? value)
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted[key] = value;

        // Act
        var exception = Should.Throw<ConfigurationFieldValidationException>(() =>
            ConfigurationValueNormalizer.Normalize(Fields, submitted)
        );

        // Assert
        exception.FieldKey.ShouldBe(key);
        exception.Error.ShouldBe(ConfigurationFieldValidationError.Required);
    }

    [Test]
    public void Normalize_RequiredKeyAbsentOnCreate_Throws()
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted.Remove("Port");

        // Act
        var exception = Should.Throw<ConfigurationFieldValidationException>(() =>
            ConfigurationValueNormalizer.Normalize(Fields, submitted)
        );

        // Assert
        exception.FieldKey.ShouldBe("Port");
        exception.Error.ShouldBe(ConfigurationFieldValidationError.Required);
    }

    [Test]
    public void Normalize_EmptyPasswordWithExistingValue_KeepsExistingPassword()
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted["Password"] = "";

        // Act
        var result = ConfigurationValueNormalizer.Normalize(Fields, submitted, ExistingValues());

        // Assert
        result["Password"].ShouldBe("old-secret");
    }

    [Test]
    public void Normalize_EmptyRequiredTextWithExistingValue_Throws()
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted["Host"] = "";

        // Act
        var exception = Should.Throw<ConfigurationFieldValidationException>(() =>
            ConfigurationValueNormalizer.Normalize(Fields, submitted, ExistingValues())
        );

        // Assert
        exception.FieldKey.ShouldBe("Host");
        exception.Error.ShouldBe(ConfigurationFieldValidationError.Required);
    }

    [Test]
    public void Normalize_EmptyOptionalTextWithExistingValue_RemovesValue()
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted["Comment"] = " ";

        // Act
        var result = ConfigurationValueNormalizer.Normalize(Fields, submitted, ExistingValues());

        // Assert
        result.ShouldNotContainKey("Comment");
    }

    [Test]
    public void Normalize_PartialUpdate_OverlaysSubmittedValuesOnExistingValues()
    {
        // Arrange
        var submitted = new Dictionary<string, object?> { ["Port"] = 990d };

        // Act
        var result = ConfigurationValueNormalizer.Normalize(Fields, submitted, ExistingValues());

        // Assert
        result["Port"].ShouldBe(990);
        result["Host"].ShouldBe("old.example.org");
        result["Password"].ShouldBe("old-secret");
        result["Comment"].ShouldBe("old comment");
    }

    [Test]
    public void Normalize_OptionNotInList_Throws()
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted["Mode"] = "secure";

        // Act
        var exception = Should.Throw<ConfigurationFieldValidationException>(() =>
            ConfigurationValueNormalizer.Normalize(Fields, submitted)
        );

        // Assert
        exception.Error.ShouldBe(ConfigurationFieldValidationError.InvalidOption);
    }

    [TestCase(21.5d)]
    [TestCase(double.NaN)]
    [TestCase(3_000_000_000d)]
    [TestCase("abc")]
    public void Normalize_NonIntegralNumber_Throws(object value)
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted["Port"] = value;

        // Act
        var exception = Should.Throw<ConfigurationFieldValidationException>(() =>
            ConfigurationValueNormalizer.Normalize(Fields, submitted)
        );

        // Assert
        exception.FieldKey.ShouldBe("Port");
        exception.Error.ShouldBe(ConfigurationFieldValidationError.NotAnInteger);
    }

    [Test]
    public void Normalize_NumberAsString_ParsesInteger()
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted["Port"] = "990";

        // Act
        var result = ConfigurationValueNormalizer.Normalize(Fields, submitted);

        // Assert
        result["Port"].ShouldBeOfType<int>().ShouldBe(990);
    }

    [TestCase("Verify", "maybe")]
    [TestCase("Host", 42)]
    public void Normalize_WrongValueType_Throws(string key, object value)
    {
        // Arrange
        var submitted = ValidSubmission();
        submitted[key] = value;

        // Act
        var exception = Should.Throw<ConfigurationFieldValidationException>(() =>
            ConfigurationValueNormalizer.Normalize(Fields, submitted)
        );

        // Assert
        exception.FieldKey.ShouldBe(key);
        exception.Error.ShouldBe(ConfigurationFieldValidationError.InvalidValue);
    }

    [Test]
    public void ValidationException_IsArgumentExceptionWithCleanMessage()
    {
        // Arrange
        var exception = new ConfigurationFieldValidationException(
            "Host",
            ConfigurationFieldValidationError.Required
        );

        // Act
        var argumentException = exception as ArgumentException;

        // Assert
        argumentException.ShouldNotBeNull();
        argumentException.ParamName.ShouldBeNull();
        argumentException.Message.ShouldBe("The configuration field 'Host' is invalid: Required.");
    }

    private static Dictionary<string, object?> ValidSubmission()
    {
        return new Dictionary<string, object?>
        {
            ["Host"] = "  ftp.example.org ",
            ["Port"] = 2121d,
            ["Password"] = " secret ",
            ["Mode"] = " Secure ",
            ["Verify"] = true,
            ["Comment"] = "",
        };
    }

    private static Dictionary<string, object?> ExistingValues()
    {
        return new Dictionary<string, object?>
        {
            ["Host"] = "old.example.org",
            ["Port"] = 21,
            ["Password"] = "old-secret",
            ["Mode"] = "Plain",
            ["Verify"] = false,
            ["Comment"] = "old comment",
        };
    }
}
