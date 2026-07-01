using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Alfafile.Api.File;

public class MoveFileResponse
{
    public ResponseObject? Response { get; set; }

    public int Status { get; set; }

    public string? Details { get; set; }

    public class ResponseObject
    {
        public ResultObject? Result { get; set; }
    }

    public class ResultObject
    {
        public int Success { get; set; }

        [JsonPropertyName("success_ids")]
        public IReadOnlyList<string> SuccessIds { get; set; } = [];

        public int Fail { get; set; }

        [JsonPropertyName("fail_ids")]
        public IReadOnlyList<string> FailIds { get; set; } = [];

        public IReadOnlyList<string> Errors { get; set; } = [];
    }
}
