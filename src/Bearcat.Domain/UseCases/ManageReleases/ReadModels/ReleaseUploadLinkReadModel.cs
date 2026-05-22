using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseUploadLinkReadModel(
    string FileName,
    string HosterFileLink,
    OnlineState OnlineState,
    DateTime? CheckedAt
);
