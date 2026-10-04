using Bearcat.Domain.Shared.ForumPostRendering;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public static class ForumPostTemplateVariableTreeFilter
{
    public static string CreateKey(string? parentKey, string name)
    {
        return parentKey is null ? name : $"{parentKey}/{name}";
    }

    public static ForumPostTemplateVariableTreeFilterResult Filter(
        IReadOnlyList<ForumPostTemplateVariableNode> nodes,
        string? searchTerm
    )
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new ForumPostTemplateVariableTreeFilterResult(nodes, new HashSet<string>());
        }

        var expandedKeys = new HashSet<string>();
        var filteredNodes = FilterNodes(nodes, null, searchTerm.Trim(), expandedKeys);

        return new ForumPostTemplateVariableTreeFilterResult(filteredNodes, expandedKeys);
    }

    private static List<ForumPostTemplateVariableNode> FilterNodes(
        IReadOnlyList<ForumPostTemplateVariableNode> nodes,
        string? parentKey,
        string searchTerm,
        HashSet<string> expandedKeys
    )
    {
        var filteredNodes = new List<ForumPostTemplateVariableNode>();

        foreach (var node in nodes)
        {
            var key = CreateKey(parentKey, node.Name);
            var matchingChildren = FilterNodes(node.Children, key, searchTerm, expandedKeys);

            if (matchingChildren.Count > 0)
            {
                expandedKeys.Add(key);
            }

            if (Matches(node, searchTerm))
            {
                filteredNodes.Add(node);
            }
            else if (matchingChildren.Count > 0)
            {
                filteredNodes.Add(node with { Children = matchingChildren });
            }
        }

        return filteredNodes;
    }

    private static bool Matches(ForumPostTemplateVariableNode node, string searchTerm)
    {
        return node.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || node.Path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || node.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }
}
