namespace Bearcat.Abstractions.LinkCrypter.Results;

public record ListedContainer(
    string ContainerId,
    string ContainerUrl,
    string ContainerName,
    string ExternalReference
);
