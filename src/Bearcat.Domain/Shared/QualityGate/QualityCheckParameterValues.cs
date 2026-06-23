using System.Text.Json;

namespace Bearcat.Domain.Shared.QualityGate;

public sealed class QualityCheckParameterValues
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyDictionary<string, JsonElement> values;

    private QualityCheckParameterValues(IReadOnlyDictionary<string, JsonElement> values)
    {
        this.values = values;
    }

    public static QualityCheckParameterValues Parse(string json)
    {
        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Options);

        return new QualityCheckParameterValues(parsed ?? []);
    }

    public static string Serialize(IReadOnlyDictionary<string, object?> values)
    {
        return JsonSerializer.Serialize(values, Options);
    }

    public string GetString(string key, string fallback = "")
    {
        return values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    public int GetInt(string key, int fallback = 0)
    {
        return values.TryGetValue(key, out var value) && value.TryGetInt32(out var number)
            ? number
            : fallback;
    }

    public bool GetBool(string key, bool fallback = false)
    {
        return
            values.TryGetValue(key, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
    }

    public object Read(QualityCheckParameterDescriptor descriptor)
    {
        return descriptor.Kind switch
        {
            QualityCheckParameterKind.Text => GetString(
                descriptor.Key,
                (string)descriptor.DefaultValue
            ),
            QualityCheckParameterKind.Integer => GetInt(
                descriptor.Key,
                (int)descriptor.DefaultValue
            ),
            QualityCheckParameterKind.Boolean => GetBool(
                descriptor.Key,
                (bool)descriptor.DefaultValue
            ),
            _ => descriptor.DefaultValue,
        };
    }
}
