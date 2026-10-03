using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageUploads;

public class UploadSearchFormModel
{
    public string? SearchTerm { get; set; }
    public UploadState? UploadState { get; set; }
    public OnlineState? OnlineState { get; set; }
    public int? HosterRegistrationId { get; set; }
    public int ReleaseGroupId { get; set; }

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm)
        || UploadState is not null
        || OnlineState is not null
        || HosterRegistrationId is not null
        || ReleaseGroupId != 0;

    public static UploadSearchFormModel FromQuery(UploadSearchQuery query)
    {
        return new UploadSearchFormModel
        {
            SearchTerm = query.SearchTerm,
            UploadState = query.UploadState,
            OnlineState = query.OnlineState,
            HosterRegistrationId = query.HosterRegistrationId,
            ReleaseGroupId = query.ReleaseGroupId ?? 0,
        };
    }

    public UploadSearchQuery ToQuery()
    {
        return new UploadSearchQuery(
            SearchTerm: SearchTerm,
            UploadState: UploadState,
            OnlineState: OnlineState,
            HosterRegistrationId: HosterRegistrationId,
            ReleaseGroupId: ReleaseGroupId == 0 ? null : ReleaseGroupId
        );
    }
}
