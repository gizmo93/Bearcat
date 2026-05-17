namespace Bearcat.Abstractions.Configurations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ApplicationConfigurationPropertyAttribute(
    string displayName,
    string? description = null
) : Attribute
{
    public string DisplayName { get; } = displayName;

    public string? Description { get; } = description;
}
