using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Website.Pages.ManageRemoteSources;
using BlazorBlueprint.Components;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageRemoteSources;

public class ConfigurationFieldFormMapperTest
{
    private const string Prefix = "Config_";

    private readonly FakeStringLocalizer localizer = new(
        new Dictionary<string, string>
        {
            ["Config_Host"] = "Host label",
            ["Config_Password"] = "Password label",
            ["Config_Mode"] = "Mode label",
            ["Config_Mode_Secure"] = "Secure label",
            ["Config_Verify_Description"] = "Verify description",
            ["SecretKeepCurrentHelp"] = "Keep help",
            ["SecretUnchangedPlaceholder"] = "Unchanged",
            ["MustBeWholeNumber"] = "Whole number",
        }
    );

    [TestCase(ConfigurationFieldType.Text, FieldType.Text)]
    [TestCase(ConfigurationFieldType.Password, FieldType.Password)]
    [TestCase(ConfigurationFieldType.Number, FieldType.Number)]
    [TestCase(ConfigurationFieldType.Boolean, FieldType.Switch)]
    [TestCase(ConfigurationFieldType.Select, FieldType.Select)]
    public void ToFormField_FieldType_MapsToBlueprintFieldType(
        ConfigurationFieldType type,
        FieldType expected
    )
    {
        // Arrange
        var field = new ConfigurationField("Key", type, IsRequired: false);

        // Act
        var result = ConfigurationFieldFormMapper.ToFormField(field, localizer, Prefix, false);

        // Assert
        result.Type.ShouldBe(expected);
    }

    [Test]
    public void ToFormField_Create_UsesLabelRequiredAndDefault()
    {
        // Arrange
        var field = new ConfigurationField(
            "Host",
            ConfigurationFieldType.Text,
            IsRequired: true,
            DefaultValue: "localhost"
        );

        // Act
        var result = ConfigurationFieldFormMapper.ToFormField(field, localizer, Prefix, false);

        // Assert
        result.Name.ShouldBe("Host");
        result.Label.ShouldBe("Host label");
        result.Description.ShouldBeNull();
        result.Required.ShouldBeTrue();
        result.DefaultValue.ShouldBe("localhost");
    }

    [Test]
    public void ToFormField_MissingLabelResource_FallsBackToKey()
    {
        // Arrange
        var field = new ConfigurationField("Verify", ConfigurationFieldType.Boolean, false);

        // Act
        var result = ConfigurationFieldFormMapper.ToFormField(field, localizer, Prefix, false);

        // Assert
        result.Label.ShouldBe("Verify");
        result.Description.ShouldBe("Verify description");
    }

    [Test]
    public void ToFormField_EditPassword_IsOptionalWithKeepHint()
    {
        // Arrange
        var field = new ConfigurationField("Password", ConfigurationFieldType.Password, true);

        // Act
        var result = ConfigurationFieldFormMapper.ToFormField(field, localizer, Prefix, true);

        // Assert
        result.Required.ShouldBeFalse();
        result.Placeholder.ShouldBe("Unchanged");
        result.Description.ShouldBe("Keep help");
    }

    [Test]
    public void ToFormField_Edit_DoesNotApplyDefaultValue()
    {
        // Arrange
        var field = new ConfigurationField(
            "Port",
            ConfigurationFieldType.Number,
            true,
            DefaultValue: 21
        );

        // Act
        var result = ConfigurationFieldFormMapper.ToFormField(field, localizer, Prefix, true);

        // Assert
        result.Required.ShouldBeTrue();
        result.DefaultValue.ShouldBeNull();
    }

    [Test]
    public void ToFormField_Select_LocalizesOptionsWithFallback()
    {
        // Arrange
        var field = new ConfigurationField(
            "Mode",
            ConfigurationFieldType.Select,
            true,
            Options: ["Plain", "Secure"]
        );

        // Act
        var result = ConfigurationFieldFormMapper.ToFormField(field, localizer, Prefix, false);

        // Assert
        result.Options.ShouldNotBeNull();
        result.Options.Select(option => option.Value).ShouldBe(["Plain", "Secure"]);
        result.Options.Select(option => option.Text).ShouldBe(["Plain", "Secure label"]);
    }

    [TestCase(21d, null)]
    [TestCase(21.5d, "Whole number")]
    public void ToFormField_Number_ValidatesWholeNumbers(double value, string? expectedError)
    {
        // Arrange
        var field = new ConfigurationField("Port", ConfigurationFieldType.Number, true);

        // Act
        var result = ConfigurationFieldFormMapper.ToFormField(field, localizer, Prefix, false);

        // Assert
        result.Validations.ShouldNotBeNull();
        result.Validations.ShouldContain(validation => validation.Type == ValidationType.Custom);
        var validator = result
            .Metadata!["customValidator"]
            .ShouldBeOfType<Func<object?, string?>>();
        validator(value).ShouldBe(expectedError);
    }

    private sealed class FakeStringLocalizer(IReadOnlyDictionary<string, string> resources)
        : IStringLocalizer
    {
        public LocalizedString this[string name] =>
            resources.TryGetValue(name, out var value)
                ? new LocalizedString(name, value)
                : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return resources.Select(entry => new LocalizedString(entry.Key, entry.Value));
        }
    }
}
