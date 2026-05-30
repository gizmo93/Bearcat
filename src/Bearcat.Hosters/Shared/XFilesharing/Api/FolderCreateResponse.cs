using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class FolderCreateResponse
{
    public int Status { get; set; }

    public string? Msg { get; set; }

    public ResultObject? Result { get; set; }

    public class ResultObject
    {
        [JsonPropertyName("fld_id")]
        [JsonConverter(typeof(StringOrNumberJsonConverter))]
        public string FolderId { get; set; } = null!;
    }
}
