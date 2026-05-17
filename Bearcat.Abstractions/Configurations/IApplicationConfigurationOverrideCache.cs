namespace Bearcat.Abstractions.Configurations;

public interface IApplicationConfigurationOverrideCache
{
    bool IsInitialized { get; }

    bool TryGetValue(
        string configurationKey,
        string propertyName,
        Type propertyType,
        out object? value
    );

    Task RefreshAsync(CancellationToken cancellationToken);

    void SetOverride(string configurationKey, string propertyName, string serializedValue);

    void RemoveOverride(string configurationKey, string propertyName);
}
