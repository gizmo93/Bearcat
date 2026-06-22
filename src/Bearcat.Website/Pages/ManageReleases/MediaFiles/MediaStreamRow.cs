namespace Bearcat.Website.Pages.ManageReleases.MediaFiles;

public sealed record MediaStreamRow(
    string Kind,
    string Codec,
    string Language,
    string Title,
    string Details,
    bool IsDefault,
    bool Forced
);
