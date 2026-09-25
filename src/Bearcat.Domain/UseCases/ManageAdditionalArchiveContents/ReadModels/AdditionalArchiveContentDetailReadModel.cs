using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;

public record AdditionalArchiveContentDetailReadModel(
    int Id,
    string Name,
    AdditionalArchiveContentType Type,
    string? SourcePath,
    string? FileName,
    string? TextContent
);
