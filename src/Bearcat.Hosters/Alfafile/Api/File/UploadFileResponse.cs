using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Alfafile.Api.File;

public class UploadFileResponse
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public ResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public Upload? Upload { get; set; }
    }

    public class Upload
    {
        [JsonPropertyName("upload_id")]
        public string UploadId { get; set; } = null!;

        public string? Url { get; set; }

        public JsonElement File { get; set; }

        public int State { get; set; }

        [JsonPropertyName("state_label")]
        public string? StateLabel { get; set; }

        public UploadedFile? GetFile()
        {
            return File.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<UploadedFile>(File.GetRawText(), JsonSerializerOptions)
                : null;
        }
    }
}
