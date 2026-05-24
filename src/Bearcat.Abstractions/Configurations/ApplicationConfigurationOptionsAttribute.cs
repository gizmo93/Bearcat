namespace Bearcat.Abstractions.Configurations;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ApplicationConfigurationOptionsAttribute(params string[] values) : Attribute
{
    public IReadOnlyList<string> Values { get; } = values;
}
