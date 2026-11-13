namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Exceptions;

public sealed class ReleaseCollectionNameAlreadyExistsException(string releaseCollectionName)
    : Exception($"A release collection with the name '{releaseCollectionName}' already exists.")
{
    public string ReleaseCollectionName { get; } = releaseCollectionName;
}
