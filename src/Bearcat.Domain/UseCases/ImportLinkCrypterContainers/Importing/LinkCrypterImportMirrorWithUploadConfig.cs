using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public sealed record LinkCrypterImportMirrorWithUploadConfig(
    LinkCrypterImportMirror Mirror,
    UploadConfig UploadConfig
);
