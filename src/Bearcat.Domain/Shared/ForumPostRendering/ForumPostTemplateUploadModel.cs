namespace Bearcat.Domain.Shared.ForumPostRendering;

/// <summary>
/// Forum post upload variables for a single upload configuration. Shared by the release and the
/// release collection render models so both expose the same upload/link-crypter variables.
/// </summary>
public sealed record ForumPostTemplateUploadModel
{
    [ForumPostTemplateVariable("Upload configuration name.")]
    public required string Name { get; init; }

    [ForumPostTemplateVariable("Hoster registration name.")]
    public required string HosterName { get; init; }

    [ForumPostTemplateVariable("Latest upload date.")]
    public required DateTime? UploadedAt { get; init; }

    [ForumPostTemplateVariable("Archive format, e.g. RAR.")]
    public required string ArchiveFormat { get; init; }

    [ForumPostTemplateVariable("Archive password (empty when the archive has no password).")]
    public required string ArchivePassword { get; init; }

    [ForumPostTemplateVariable(
        "Loop over direct hoster links of the latest upload.",
        LoopVariable = "link"
    )]
    public required IReadOnlyList<string> Links { get; init; }

    [ForumPostTemplateVariable(
        "Loop over link crypter container links.",
        LoopVariable = "crypter",
        ElementType = typeof(ForumPostTemplateLinkCrypterModel)
    )]
    public required IReadOnlyList<ForumPostTemplateLinkCrypterModel> LinkCrypters { get; init; }
}

public sealed record ForumPostTemplateLinkCrypterModel
{
    [ForumPostTemplateVariable("Link crypter registration name.")]
    public required string Name { get; init; }

    [ForumPostTemplateVariable("Link crypter password (empty when not set).")]
    public required string Password { get; init; }

    [ForumPostTemplateVariable("Generated container URL.")]
    public required string ContainerLink { get; init; }

    [ForumPostTemplateVariable("Container creation date.")]
    public required DateTime CreatedAt { get; init; }
}
