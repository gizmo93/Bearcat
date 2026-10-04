using Bearcat.Domain.Shared.ForumPostRendering;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public record ForumPostTemplateVariableTreeFilterResult(
    IReadOnlyList<ForumPostTemplateVariableNode> Nodes,
    IReadOnlySet<string> ExpandedKeys
);
