using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Dto;

public record AdditionalArchiveContentInput(
    string Name,
    AdditionalArchiveContentType Type,
    string? SourcePath,
    string? FileName,
    string? TextContent
);
