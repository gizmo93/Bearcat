using Bearcat.Abstractions.Configurations;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Configurations;

public class ApplicationConfigurationRegistryTest
{
    [Test]
    public void GetDefinition_PropertyWithUnitAttribute_ReadsUnit()
    {
        // Arrange
        var registry = new ApplicationConfigurationRegistry([
            new ApplicationConfigurationRegistration(typeof(ConfigurationWithUnits)),
        ]);

        // Act
        var definition = registry.GetDefinition<ConfigurationWithUnits>();

        // Assert
        definition
            .Properties.Single(property =>
                property.Name == nameof(ConfigurationWithUnits.RetentionDays)
            )
            .Unit.ShouldBe(ApplicationConfigurationUnit.Days);
        definition
            .Properties.Single(property =>
                property.Name == nameof(ConfigurationWithUnits.SpeedLimitMegabytesPerSecond)
            )
            .Unit.ShouldBe(ApplicationConfigurationUnit.MegabytesPerSecond);
    }

    [Test]
    public void GetDefinition_PropertyWithoutUnitAttribute_HasNoUnit()
    {
        // Arrange
        var registry = new ApplicationConfigurationRegistry([
            new ApplicationConfigurationRegistration(typeof(ConfigurationWithUnits)),
        ]);

        // Act
        var definition = registry.GetDefinition<ConfigurationWithUnits>();

        // Assert
        definition
            .Properties.Single(property =>
                property.Name == nameof(ConfigurationWithUnits.MaxParallelOperations)
            )
            .Unit.ShouldBeNull();
    }

    [Test]
    public void GetDefinition_EnumProperty_IsSupportedWithEnumNamesAsOptions()
    {
        // Arrange
        var registry = new ApplicationConfigurationRegistry([
            new ApplicationConfigurationRegistration(typeof(ConfigurationWithEnum)),
        ]);

        // Act
        var definition = registry.GetDefinition<ConfigurationWithEnum>();

        // Assert
        var property = definition.Properties.ShouldHaveSingleItem();
        property.Name.ShouldBe(nameof(ConfigurationWithEnum.Mode));
        property.PropertyType.ShouldBe(typeof(ConfigurationMode));
        property.Options.ShouldBe(["Slow", "Fast"]);
    }

    [ApplicationConfiguration("ConfigurationWithEnum", "ConfigurationWithEnum")]
    public sealed class ConfigurationWithEnum : IApplicationConfiguration
    {
        public ConfigurationMode Mode { get; set; } = ConfigurationMode.Slow;

        public ConfigurationMode? OptionalMode { get; set; }
    }

    public enum ConfigurationMode
    {
        Slow = 1,
        Fast = 2,
    }

    [ApplicationConfiguration("ConfigurationWithUnits", "ConfigurationWithUnits")]
    public sealed class ConfigurationWithUnits : IApplicationConfiguration
    {
        [ApplicationConfigurationUnit(ApplicationConfigurationUnit.Days)]
        public int RetentionDays { get; set; } = 14;

        [ApplicationConfigurationUnit(ApplicationConfigurationUnit.MegabytesPerSecond)]
        public decimal? SpeedLimitMegabytesPerSecond { get; set; }

        public int MaxParallelOperations { get; set; } = 2;
    }
}
