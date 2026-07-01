using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class UpdateFilesResponse
{
    public string? Status { get; init; }

    public int Code { get; init; }

    public string? Message { get; init; }

    [JsonPropertyName("files")]
    public List<UpdatedFile> Files { get; init; } = [];

    public class UpdatedFile
    {
        public string? Id { get; init; }

        public string? Status { get; init; }

        [JsonPropertyName("errors")]
        public List<string> Errors { get; init; } = [];
    }
}
