using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record CompleteUploadRequest([property: JsonPropertyName("sessionId")] string SessionId);
