using System.Linq.Expressions;

namespace Bearcat.Abstractions.Configurations;

public class ApplicationConfigurationProvider(
    ApplicationConfigurationRegistry registry,
    IApplicationConfigurationOverrideCache overrideCache
) : IApplicationConfigurationProvider
{
    public TConfiguration GetConfiguration<TConfiguration>()
        where TConfiguration : IApplicationConfiguration, new()
    {
        var configuration = new TConfiguration();
        var definition = registry.GetDefinition<TConfiguration>();

        foreach (var property in definition.Properties)
        {
            if (
                !overrideCache.TryGetValue(
                    definition.Key,
                    property.Name,
                    property.PropertyType,
                    out var value
                )
            )
            {
                continue;
            }

            property.PropertyInfo.SetValue(configuration, value);
        }

        return configuration;
    }

    public bool GetValue<TConfiguration>(Expression<Func<TConfiguration, bool>> propertySelector)
        where TConfiguration : IApplicationConfiguration, new()
    {
        return GetValue<TConfiguration, bool>(propertySelector);
    }

    public int GetValue<TConfiguration>(Expression<Func<TConfiguration, int>> propertySelector)
        where TConfiguration : IApplicationConfiguration, new()
    {
        return GetValue<TConfiguration, int>(propertySelector);
    }

    public int? GetValue<TConfiguration>(Expression<Func<TConfiguration, int?>> propertySelector)
        where TConfiguration : IApplicationConfiguration, new()
    {
        return GetValue<TConfiguration, int?>(propertySelector);
    }

    public string? GetValue<TConfiguration>(
        Expression<Func<TConfiguration, string?>> propertySelector
    )
        where TConfiguration : IApplicationConfiguration, new()
    {
        return GetValue<TConfiguration, string?>(propertySelector);
    }

    public TValue GetValue<TConfiguration, TValue>(
        Expression<Func<TConfiguration, TValue>> propertySelector
    )
        where TConfiguration : IApplicationConfiguration, new()
    {
        var propertyName = GetPropertyName(propertySelector);
        var definition = registry.GetDefinition<TConfiguration>();
        var property =
            definition.Properties.FirstOrDefault(p => p.Name == propertyName)
            ?? throw new InvalidOperationException(
                $"Property {propertyName} is not a supported configuration property."
            );

        if (
            overrideCache.TryGetValue(
                definition.Key,
                property.Name,
                property.PropertyType,
                out var value
            )
        )
        {
            return (TValue)value!;
        }

        return propertySelector.Compile()(new TConfiguration());
    }

    private static string GetPropertyName<TConfiguration, TValue>(
        Expression<Func<TConfiguration, TValue>> propertySelector
    )
    {
        return propertySelector.Body switch
        {
            MemberExpression memberExpression => memberExpression.Member.Name,
            UnaryExpression { Operand: MemberExpression memberExpression } => memberExpression
                .Member
                .Name,
            _ => throw new InvalidOperationException(
                "Configuration value selectors must target a single property."
            ),
        };
    }
}
