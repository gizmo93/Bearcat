namespace Bearcat.Abstractions.Configurations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ApplicationConfigurationAttribute(
    string key,
    string displayName,
    string? description = null
) : Attribute
{
    public string Key { get; } = key;

    public string DisplayName { get; } = displayName;

    public string? Description { get; } = description;
}
