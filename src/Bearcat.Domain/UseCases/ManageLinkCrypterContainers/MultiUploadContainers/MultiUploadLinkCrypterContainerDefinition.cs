using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers.MultiUploadContainers;

public sealed record MultiUploadLinkCrypterContainerDefinition(
    string ContainerDescriptionInMessages,
    string OwnerDescriptionInMessages,
    string ContainerName,
    NotificationKind InvalidNotificationKind,
    Action ResetUploadsPostedAt,
    UploadConfigLinkCrypter LinkCrypterConfig,
    IReadOnlyList<Upload> Uploads,
    IReadOnlyDictionary<int, IReadOnlyList<string>> LinksPerMirrorNumber
);
