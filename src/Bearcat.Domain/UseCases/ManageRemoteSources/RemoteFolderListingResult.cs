using Bearcat.Abstractions.RemoteSource.Dto;

namespace Bearcat.Domain.UseCases.ManageRemoteSources;

public record RemoteFolderListingResult(
    bool IsSuccess,
    string? ErrorMessage,
    IReadOnlyList<RemoteFolderDto> Folders
);
