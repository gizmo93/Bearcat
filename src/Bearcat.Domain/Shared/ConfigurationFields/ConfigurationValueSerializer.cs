using System.Text.Json;

namespace Bearcat.Domain.Shared.ConfigurationFields;

public static class ConfigurationValueSerializer
{
    public static string Serialize(IReadOnlyDictionary<string, object?> values)
    {
        return JsonSerializer.Serialize(values);
    }
}
