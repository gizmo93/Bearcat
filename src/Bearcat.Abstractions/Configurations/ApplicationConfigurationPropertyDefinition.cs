using System.Reflection;

namespace Bearcat.Abstractions.Configurations;

public sealed record ApplicationConfigurationPropertyDefinition(
    string Name,
    string DisplayName,
    string? Description,
    Type PropertyType,
    PropertyInfo PropertyInfo,
    IReadOnlyList<string> Options,
    ApplicationConfigurationUnit? Unit
);
