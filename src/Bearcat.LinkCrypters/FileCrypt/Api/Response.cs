using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.FileCrypt.Api;

public class Response
{
    [JsonPropertyName("state")]
    public int State { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("container")]
    [JsonConverter(typeof(ContainerResponseListJsonConverter))]
    public List<ContainerResponse>? Container { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }
}

public class ContainerResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class ContainerResponseListJsonConverter : JsonConverter<List<ContainerResponse>>
{
    public override List<ContainerResponse>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray()
                .Select(element => element.Deserialize<ContainerResponse>(options))
                .Where(container => container is not null)
                .Select(container => container!)
                .ToList(),
            JsonValueKind.Object when IsContainerObject(root) =>
            [
                root.Deserialize<ContainerResponse>(options)!,
            ],
            JsonValueKind.Object => root.EnumerateObject()
                .Select(property => property.Value.Deserialize<ContainerResponse>(options))
                .Where(container => container is not null)
                .Select(container => container!)
                .ToList(),
            _ => [],
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<ContainerResponse> value,
        JsonSerializerOptions options
    )
    {
        JsonSerializer.Serialize(writer, value, options);
    }

    private static bool IsContainerObject(JsonElement element) =>
        element.TryGetProperty("link", out _)
        || element.TryGetProperty("id", out _)
        || element.TryGetProperty("name", out _);
}
