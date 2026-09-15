using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.CollectionAssignment;

public interface IReleaseCollectionAssigner
{
    Task AssignFromTemplateAsync(
        Release release,
        ReleaseTemplate releaseTemplate,
        IReadOnlyList<ReleaseUploadConfigMatch> uploadConfigMatches,
        CancellationToken cancellationToken = default
    );
}
