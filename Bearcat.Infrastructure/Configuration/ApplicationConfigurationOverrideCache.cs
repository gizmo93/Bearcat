using System.Collections.Concurrent;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Infrastructure.Configuration;

public class ApplicationConfigurationOverrideCache(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ApplicationConfigurationOverrideCache> logger
) : IApplicationConfigurationOverrideCache
{
    private readonly ConcurrentDictionary<
        (string ConfigurationKey, string PropertyName),
        string
    > overrides = [];

    public bool IsInitialized { get; private set; }

    public bool TryGetValue(
        string configurationKey,
        string propertyName,
        Type propertyType,
        out object? value
    )
    {
        value = null;

        if (!overrides.TryGetValue((configurationKey, propertyName), out var serializedValue))
        {
            return false;
        }

        try
        {
            value = ApplicationConfigurationValueSerializer.Deserialize(
                serializedValue,
                propertyType
            );
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not deserialize application configuration override {ConfigurationKey}.{PropertyName}",
                configurationKey,
                propertyName
            );

            return false;
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var readRepository =
            scope.ServiceProvider.GetRequiredService<IApplicationConfigurationOverrideReadRepository>();
        var loadedOverrides = await readRepository.GetAllAsync(cancellationToken);
        overrides.Clear();

        foreach (var configurationOverride in loadedOverrides)
        {
            overrides[
                (configurationOverride.ConfigurationKey, configurationOverride.PropertyName)
            ] = configurationOverride.SerializedValue;
        }

        IsInitialized = true;
    }

    public void SetOverride(string configurationKey, string propertyName, string serializedValue)
    {
        overrides[(configurationKey, propertyName)] = serializedValue;
        IsInitialized = true;
    }

    public void RemoveOverride(string configurationKey, string propertyName)
    {
        overrides.TryRemove((configurationKey, propertyName), out _);
    }
}
