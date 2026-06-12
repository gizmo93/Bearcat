using Bearcat.Domain.ValueObjects;
using Scriban.Runtime;

namespace Bearcat.Domain.Shared.ForumPostRendering;

/// <summary>
/// Provides the available template variables and the render data for a single
/// <see cref="ForumPostTemplateType"/>. Each supported type isolates its own data preloading.
/// </summary>
public interface IForumPostRenderSource
{
    ForumPostTemplateType Type { get; }

    IReadOnlyList<ForumPostTemplateVariableReadModel> GetVariables();

    Task<ScriptObject?> BuildGlobalsAsync(
        int entityId,
        CancellationToken cancellationToken = default
    );
}
