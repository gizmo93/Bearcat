using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public sealed record LinkCrypterImportContainerPreview(
    IReadOnlyList<LinkCrypterImportMirror> Mirrors,
    string? ReleaseFolderPath,
    string? ArchiveFolderPath,
    LinkCrypterImportReleaseAction? ReleaseAction,
    int? ExistingReleaseId
)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(
        JsonSerializerDefaults.Web
    )
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }

    public static LinkCrypterImportContainerPreview FromJson(string json)
    {
        return JsonSerializer.Deserialize<LinkCrypterImportContainerPreview>(
            json,
            JsonSerializerOptions
        )!;
    }
}
