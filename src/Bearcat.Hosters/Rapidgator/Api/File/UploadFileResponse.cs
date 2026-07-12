using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Rapidgator.Api.File;

public class UploadFileResponse
{
    public ResponseObject? Response { get; set; } = null!;

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public Upload? Upload { get; set; }

        public File? File { get; set; }

        public int State { get; set; }
    }

    public class Upload
    {
        [JsonPropertyName("upload_id")]
        public string UploadId { get; set; } = null!;

        public string Url { get; set; } = null!;

        [JsonConverter(typeof(FileOrEmptyArrayConverter))]
        public File? File { get; set; }

        public int State { get; set; }

        [JsonPropertyName("state_label")]
        public string StateLabel { get; set; } = null!;

        public UploadError? Error { get; set; }
    }

    public class UploadError
    {
        public int Code { get; set; }

        public string? Message { get; set; }
    }

    public class File
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = null!;

        public int Mode { get; set; }

        [JsonPropertyName("mode_label")]
        public string ModeLabel { get; set; } = null!;

        [JsonPropertyName("folder_id")]
        public string? FolderId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        public string Hash { get; set; } = null!;

        public long Size { get; set; }

        public long Created { get; set; }

        public string Url { get; set; } = null!;
    }

    public sealed class FileOrEmptyArrayConverter : JsonConverter<File?>
    {
        public override File? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                using var document = JsonDocument.ParseValue(ref reader);

                if (document.RootElement.GetArrayLength() != 0)
                {
                    throw new JsonException("Rapidgator upload file array must be empty");
                }

                return null;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return JsonSerializer.Deserialize<File>(ref reader, options);
            }

            throw new JsonException("Rapidgator upload file must be an object or an empty array");
        }

        public override void Write(
            Utf8JsonWriter writer,
            File? value,
            JsonSerializerOptions options
        )
        {
            if (value is null)
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
                return;
            }

            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
