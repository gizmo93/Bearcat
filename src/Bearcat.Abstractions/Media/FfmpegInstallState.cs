namespace Bearcat.Abstractions.Media;

public sealed record FfmpegInstallState(bool IsRunning, string? LastErrorMessage);
