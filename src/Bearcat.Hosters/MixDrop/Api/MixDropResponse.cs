using System.Text.Json;

namespace Bearcat.Hosters.MixDrop.Api;

public record MixDropResponse(bool Success, JsonElement Result);
