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
