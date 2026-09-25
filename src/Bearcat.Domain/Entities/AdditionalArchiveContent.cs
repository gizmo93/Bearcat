using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class AdditionalArchiveContent
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public AdditionalArchiveContentType Type { get; set; }

    public string? SourcePath { get; set; }

    public string? FileName { get; set; }

    public string? TextContent { get; set; }
}
