namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public record XFilesharingUploadOptions(
    bool AddRegisteredUserTypeField,
    bool AddUploadTypeQueryString,
    bool ForceHttpUploadScheme,
    string UserTypeFieldValue = "reg"
);
