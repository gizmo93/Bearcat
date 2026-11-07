namespace Bearcat.Website.Shared.UploadTargets;

public sealed record UploadTargetHoster(
    string HosterClassName,
    string HosterName,
    bool IsVideoHoster
);
