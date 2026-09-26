namespace Bearcat.Domain.UseCases.ManageReleases.Exceptions;

public sealed class ReleaseTemplateNotFoundException(int releaseTemplateId)
    : Exception($"Release template {releaseTemplateId} does not exist.")
{
    public int ReleaseTemplateId { get; } = releaseTemplateId;
}
