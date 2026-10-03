namespace Bearcat.Website.Pages.ManageUploads;

public interface IUploadSearchUrlValues
{
    string? SearchTerm { get; }
    string? UploadState { get; }
    string? OnlineState { get; }
    int? HosterRegistrationId { get; }
    int? ReleaseGroupId { get; }
    int? Page { get; }
    int? PageSize { get; }
}
