using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class FolderListResponse
{
    public int Status { get; set; }

    public string? Msg { get; set; }

    public ResultObject? Result { get; set; }

    public class ResultObject
    {
        public List<Folder> Folders { get; set; } = [];
    }

    public class Folder
    {
        public string Name { get; set; } = null!;

        [JsonPropertyName("fld_id")]
        [JsonConverter(typeof(StringOrNumberJsonConverter))]
        public string FolderId { get; set; } = null!;
    }
}
