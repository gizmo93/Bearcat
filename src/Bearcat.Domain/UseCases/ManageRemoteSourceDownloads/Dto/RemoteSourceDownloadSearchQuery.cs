using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Dto;

public record RemoteSourceDownloadSearchQuery(
    IReadOnlyList<RemoteSourceDownloadState> States,
    int PageIndex = 0,
    int PageSize = 25
);
