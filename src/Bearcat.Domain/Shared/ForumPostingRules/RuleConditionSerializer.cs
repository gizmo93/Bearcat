using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class RuleConditionSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(RuleCondition condition)
    {
        return JsonSerializer.Serialize(condition, Options);
    }

    public static RuleCondition Deserialize(string json)
    {
        return JsonSerializer.Deserialize<RuleCondition>(json, Options)!;
    }

    public static bool TryDeserialize(
        string? json,
        [NotNullWhen(true)] out RuleCondition? condition
    )
    {
        condition = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            condition = JsonSerializer.Deserialize<RuleCondition>(json, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        return condition is not null;
    }
}
