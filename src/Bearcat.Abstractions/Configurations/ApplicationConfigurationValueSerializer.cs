using System.Text.Json;

namespace Bearcat.Abstractions.Configurations;

public static class ApplicationConfigurationValueSerializer
{
    public static string Serialize(object? value, Type valueType)
    {
        return JsonSerializer.Serialize(value, valueType);
    }

    public static object? Deserialize(string serializedValue, Type valueType)
    {
        return JsonSerializer.Deserialize(serializedValue, valueType);
    }
}
