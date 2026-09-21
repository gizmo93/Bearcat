using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;

public sealed record MirrorSource(
    HosterRegistration Registration,
    Upload Upload,
    UploadedFile UploadedFile
);
