using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageAdditionalArchiveContents;

public class AdditionalArchiveContentFormModel
{
    public int? AdditionalArchiveContentId { get; set; }

    public string? Name { get; set; }

    public AdditionalArchiveContentType Type { get; set; } = AdditionalArchiveContentType.Path;

    public string? SourcePath { get; set; }

    public string? FileName { get; set; }

    public string? TextContent { get; set; }

    public AdditionalArchiveContentInput ToInput()
    {
        return new AdditionalArchiveContentInput(
            Name ?? string.Empty,
            Type,
            SourcePath,
            FileName,
            TextContent
        );
    }
}
