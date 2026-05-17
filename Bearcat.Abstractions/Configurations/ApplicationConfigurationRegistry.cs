using System.Reflection;

namespace Bearcat.Abstractions.Configurations;

public class ApplicationConfigurationRegistry
{
    private readonly IReadOnlyList<ApplicationConfigurationDefinition> definitions;

    public ApplicationConfigurationRegistry(
        IEnumerable<ApplicationConfigurationRegistration> registrations
    )
    {
        definitions = registrations
            .Select(r => r.ConfigurationType)
            .Distinct()
            .Select(CreateDefinition)
            .OrderBy(d => d.DisplayName)
            .ToList();

        var duplicateKey = definitions.GroupBy(d => d.Key).FirstOrDefault(g => g.Count() > 1)?.Key;

        if (duplicateKey is not null)
        {
            throw new InvalidOperationException(
                $"Application configuration key {duplicateKey} is registered more than once."
            );
        }
    }

    public IReadOnlyList<ApplicationConfigurationDefinition> GetDefinitions()
    {
        return definitions;
    }

    public ApplicationConfigurationDefinition GetDefinition<TConfiguration>()
        where TConfiguration : IApplicationConfiguration, new()
    {
        return GetDefinition(typeof(TConfiguration));
    }

    public ApplicationConfigurationDefinition GetDefinition(string key)
    {
        return definitions.FirstOrDefault(d => d.Key == key)
            ?? throw new InvalidOperationException($"Configuration {key} is not registered.");
    }

    private ApplicationConfigurationDefinition GetDefinition(Type configurationType)
    {
        return definitions.FirstOrDefault(d => d.ConfigurationType == configurationType)
            ?? throw new InvalidOperationException(
                $"Configuration type {configurationType.Name} is not registered."
            );
    }

    private static ApplicationConfigurationDefinition CreateDefinition(Type type)
    {
        if (
            !typeof(IApplicationConfiguration).IsAssignableFrom(type)
            || type is not { IsClass: true, IsAbstract: false }
        )
        {
            throw new InvalidOperationException(
                $"Type {type.Name} is not a concrete application configuration."
            );
        }

        var attribute = type.GetCustomAttribute<ApplicationConfigurationAttribute>();
        var key = attribute?.Key ?? type.FullName ?? type.Name;
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.CanWrite && IsSupportedType(p.PropertyType))
            .Select(p =>
            {
                var propertyAttribute =
                    p.GetCustomAttribute<ApplicationConfigurationPropertyAttribute>();

                return new ApplicationConfigurationPropertyDefinition(
                    Name: p.Name,
                    DisplayName: propertyAttribute?.DisplayName ?? p.Name,
                    Description: propertyAttribute?.Description,
                    PropertyType: p.PropertyType,
                    PropertyInfo: p
                );
            })
            .OrderBy(p => p.DisplayName)
            .ToList();

        return new ApplicationConfigurationDefinition(
            Key: key,
            DisplayName: attribute?.DisplayName ?? type.Name,
            Description: attribute?.Description,
            ConfigurationType: type,
            Properties: properties
        );
    }

    private static bool IsSupportedType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType == typeof(bool)
            || underlyingType == typeof(int)
            || underlyingType == typeof(string);
    }
}

public sealed record ApplicationConfigurationDefinition(
    string Key,
    string DisplayName,
    string? Description,
    Type ConfigurationType,
    IReadOnlyList<ApplicationConfigurationPropertyDefinition> Properties
);

public sealed record ApplicationConfigurationPropertyDefinition(
    string Name,
    string DisplayName,
    string? Description,
    Type PropertyType,
    PropertyInfo PropertyInfo
);
