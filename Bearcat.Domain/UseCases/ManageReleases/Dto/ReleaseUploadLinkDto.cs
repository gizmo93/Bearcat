using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseUploadLinkDto(
    string FileName,
    string HosterFileLink,
    OnlineState OnlineState,
    DateTime? CheckedAt
);
