namespace Bearcat.Domain.UseCases.ManageReleases.Exceptions;

public sealed class ReleaseNameAlreadyExistsException(string releaseName)
    : Exception($"A release with the name '{releaseName}' already exists.")
{
    public string ReleaseName { get; } = releaseName;
}
