using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Streamtape.Api;

public record FileInfoResult([property: JsonPropertyName("status")] int Status);
