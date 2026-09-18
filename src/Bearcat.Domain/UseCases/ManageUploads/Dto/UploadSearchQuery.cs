using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads.Dto;

public record UploadSearchQuery(
    DateTime? UploadedAfter = null,
    UploadState? UploadState = null,
    OnlineState? OnlineState = null,
    int? HosterRegistrationId = null,
    int? ReleaseId = null,
    int PageIndex = 0,
    int PageSize = 10
);
