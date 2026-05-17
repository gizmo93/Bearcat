using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Entities;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageApplicationConfigurations;

public class ApplicationConfigurationService(
    ApplicationConfigurationRegistry registry,
    IApplicationConfigurationOverrideReadRepository readRepository,
    IApplicationConfigurationOverrideWriteRepository writeRepository,
    IApplicationConfigurationOverrideCache overrideCache,
    TimeProvider timeProvider
)
{
    public async Task<IReadOnlyList<ApplicationConfigurationDto>> GetAllAsync(
        CancellationToken cancellationToken
    )
    {
        var overrides = await readRepository.GetAllAsync(cancellationToken);
        var overridesByKey = overrides.ToDictionary(
            o => (o.ConfigurationKey, o.PropertyName),
            o => o
        );

        return registry
            .GetDefinitions()
            .Select(definition =>
            {
                var defaults =
                    Activator.CreateInstance(definition.ConfigurationType)
                    ?? throw new InvalidOperationException(
                        $"Could not create default configuration {definition.ConfigurationType.Name}."
                    );

                var properties = definition
                    .Properties.Select(property =>
                    {
                        var defaultValue = property.PropertyInfo.GetValue(defaults);
                        var currentValue = defaultValue;
                        var isOverridden = overridesByKey.TryGetValue(
                            (definition.Key, property.Name),
                            out var configurationOverride
                        );

                        if (isOverridden && configurationOverride is not null)
                        {
                            currentValue = ApplicationConfigurationValueSerializer.Deserialize(
                                configurationOverride.SerializedValue,
                                property.PropertyType
                            );
                        }

                        return new ApplicationConfigurationPropertyDto(
                            ConfigurationKey: definition.Key,
                            Name: property.Name,
                            DisplayName: property.DisplayName,
                            Description: property.Description,
                            ValueType: property.PropertyType,
                            DefaultValue: defaultValue,
                            CurrentValue: currentValue,
                            IsOverridden: isOverridden
                        );
                    })
                    .ToList();

                return new ApplicationConfigurationDto(
                    DisplayName: definition.DisplayName,
                    Description: definition.Description,
                    Properties: properties
                );
            })
            .ToList();
    }

    public async Task SaveOverrideAsync(
        string configurationKey,
        string propertyName,
        object? value,
        CancellationToken cancellationToken
    )
    {
        var definition = registry.GetDefinition(configurationKey);
        var property =
            definition.Properties.FirstOrDefault(p => p.Name == propertyName)
            ?? throw new InvalidOperationException(
                $"Configuration property {configurationKey}.{propertyName} is not registered."
            );

        var serializedValue = ApplicationConfigurationValueSerializer.Serialize(
            value,
            property.PropertyType
        );
        var configurationOverride = await writeRepository.GetAsync(
            configurationKey: configurationKey,
            propertyName: propertyName,
            cancellationToken: cancellationToken
        );

        if (configurationOverride is null)
        {
            configurationOverride = new ApplicationConfigurationOverride
            {
                ConfigurationKey = configurationKey,
                PropertyName = propertyName,
                SerializedValue = serializedValue,
                UpdatedAt = timeProvider.GetLocalNow(),
            };
            writeRepository.Add(configurationOverride);
        }
        else
        {
            configurationOverride.SerializedValue = serializedValue;
            configurationOverride.UpdatedAt = timeProvider.GetLocalNow();
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
        overrideCache.SetOverride(configurationKey, propertyName, serializedValue);
    }

    public async Task ResetOverrideAsync(
        string configurationKey,
        string propertyName,
        CancellationToken cancellationToken
    )
    {
        registry.GetDefinition(configurationKey);
        var configurationOverride = await writeRepository.GetAsync(
            configurationKey: configurationKey,
            propertyName: propertyName,
            cancellationToken: cancellationToken
        );

        if (configurationOverride is null)
        {
            overrideCache.RemoveOverride(configurationKey, propertyName);
            return;
        }

        writeRepository.Remove(configurationOverride);
        await writeRepository.SaveChangesAsync(cancellationToken);
        overrideCache.RemoveOverride(configurationKey, propertyName);
    }
}
