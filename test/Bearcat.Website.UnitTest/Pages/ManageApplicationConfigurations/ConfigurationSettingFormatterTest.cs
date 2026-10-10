using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageApplicationConfigurations;

public class ConfigurationSettingFormatterTest
{
    [Test]
    public void FormatValue_EnumSetting_UsesEnumTypeNameAsResourceKeyPrefix()
    {
        // Arrange
        var localizer = new DictionaryStringLocalizer(
            new Dictionary<string, string>
            {
                ["ArchiveRetentionAction.MoveToStorageFolder"] = "Move to storage folder",
            }
        );
        var setting = CreateEnumSetting();

        // Act
        var result = ConfigurationSettingFormatter.FormatValue(
            localizer,
            setting,
            ArchiveRetentionAction.MoveToStorageFolder
        );

        // Assert
        ConfigurationSettingFormatter.HasOptions(setting).ShouldBeTrue();
        result.ShouldBe("Move to storage folder");
    }

    [Test]
    public void ParseOptionValue_EnumSetting_ReturnsEnumValue()
    {
        // Act
        var result = ConfigurationSettingFormatter.ParseOptionValue(CreateEnumSetting(), "Delete");

        // Assert
        result.ShouldBe(ArchiveRetentionAction.Delete);
    }

    private static ApplicationConfigurationPropertyDto CreateEnumSetting()
    {
        return new ApplicationConfigurationPropertyDto(
            ConfigurationKey: "ArchiveCleanup",
            Name: "ArchiveRetentionAction",
            DisplayName: "ArchiveRetentionAction",
            Description: null,
            ValueType: typeof(ArchiveRetentionAction),
            DefaultValue: ArchiveRetentionAction.Off,
            CurrentValue: ArchiveRetentionAction.Off,
            IsOverridden: false,
            Options: ["Off", "Delete", "MoveToStorageFolder"],
            Unit: null
        );
    }

    private sealed class DictionaryStringLocalizer(Dictionary<string, string> values)
        : IStringLocalizer<UiResource>
    {
        public LocalizedString this[string name] =>
            values.TryGetValue(name, out var value)
                ? new LocalizedString(name, value)
                : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            values.Select(pair => new LocalizedString(pair.Key, pair.Value));
    }
}
