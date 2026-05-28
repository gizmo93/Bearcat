using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class GetFilesInfoResponse
{
    public string Status { get; set; } = null!;

    public int Code { get; set; }

    public List<FileInfo> Files { get; set; } = [];

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    public string? Message { get; set; }

    public class FileInfo
    {
        public string Id { get; set; } = null!;

        [JsonPropertyName("is_available")]
        public bool? IsAvailable { get; set; }
    }
}
