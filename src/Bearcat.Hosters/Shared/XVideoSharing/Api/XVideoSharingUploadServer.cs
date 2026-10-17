namespace Bearcat.Hosters.Shared.XVideoSharing.Api;

public record XVideoSharingUploadServer(
    string UploadUrl,
    string ApiKeyFieldName,
    string FileFieldName
);
