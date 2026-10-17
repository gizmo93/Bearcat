using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseUploadLinkReadModel(
    string FileName,
    string HosterFileLink,
    string? HosterEmbedLink,
    OnlineState OnlineState,
    DateTime? CheckedAt,
    IReadOnlyList<string> ErrorMessages,
    int? DownloadCount
);
