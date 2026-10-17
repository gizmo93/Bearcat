using System.Text.Json;

namespace Bearcat.Hosters.Streamtape.Api;

public record StreamtapeResponse(int Status, string? Msg, JsonElement Result);
