using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class StringOrNumberJsonConverter : JsonConverter<string>
{
    public override string Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number when reader.TryGetInt64(out var value) => value.ToString(
                CultureInfo.InvariantCulture
            ),
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            _ => throw new JsonException(
                $"Cannot convert JSON token {reader.TokenType} to string."
            ),
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
