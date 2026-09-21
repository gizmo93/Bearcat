using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public sealed record ResolvedMirrorHoster(
    HosterRegistration Registration,
    IHosterWithDownload Hoster,
    IHosterConfig Config
);
