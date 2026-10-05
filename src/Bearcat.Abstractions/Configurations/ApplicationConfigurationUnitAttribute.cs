namespace Bearcat.Abstractions.Configurations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ApplicationConfigurationUnitAttribute(ApplicationConfigurationUnit unit)
    : Attribute
{
    public ApplicationConfigurationUnit Unit { get; } = unit;
}
