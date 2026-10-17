namespace Bearcat.Hosters.Vixeo.Api;

public record UploadVideoResponse(bool Success, string? Message, UploadedVideoResult? Data);
