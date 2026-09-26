namespace Bearcat.Api.Contracts.Releases;

public record CreateReleaseRequest
{
    /// <summary>
    /// Absolute path of the finished release folder as seen by the Bearcat process (the container path when Bearcat runs in Docker). Surrounding whitespace and a trailing directory separator are removed.
    /// </summary>
    public required string FolderPath { get; init; }

    /// <summary>
    /// The release template the release is created from.
    /// </summary>
    public required int ReleaseTemplateId { get; init; }

    /// <summary>
    /// Name of the release. Defaults to the folder name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Two-letter ISO 639-1 code of the primary language, for example "de". Stored in lowercase.
    /// </summary>
    public string? PrimaryLanguageCode { get; init; }
}
