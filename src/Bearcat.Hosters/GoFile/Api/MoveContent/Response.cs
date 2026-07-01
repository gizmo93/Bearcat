using System.Text.Json.Serialization;

namespace Bearcat.Hosters.GoFile.Api.MoveContent;

public class Response
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;
}
