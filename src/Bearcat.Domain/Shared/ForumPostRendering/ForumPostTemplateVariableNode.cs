namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ForumPostTemplateVariableNode(
    string Name,
    string Path,
    string Description,
    ForumPostTemplateVariableInsertion? Insertion,
    string? LoopStatement,
    IReadOnlyList<ForumPostTemplateVariableNode> Children
)
{
    public static ForumPostTemplateVariableNode CreateValueNode(
        string name,
        string path,
        string description
    )
    {
        var text = $"{{{{ {path} }}}}";

        return new ForumPostTemplateVariableNode(
            name,
            path,
            description,
            new ForumPostTemplateVariableInsertion(text, text.Length),
            LoopStatement: null,
            Children: []
        );
    }

    public static ForumPostTemplateVariableNode CreateObjectNode(
        string name,
        string path,
        string description,
        IReadOnlyList<ForumPostTemplateVariableNode> children
    )
    {
        return new ForumPostTemplateVariableNode(
            name,
            path,
            description,
            Insertion: null,
            LoopStatement: null,
            children
        );
    }

    public static ForumPostTemplateVariableNode CreateLoopNode(
        string name,
        string path,
        string description,
        string loopVariable,
        IReadOnlyList<ForumPostTemplateVariableNode> children
    )
    {
        var loopStatement = $"for {loopVariable} in {path}";
        var openingLine = $"{{{{ {loopStatement} }}}}\n";

        return new ForumPostTemplateVariableNode(
            name,
            path,
            description,
            new ForumPostTemplateVariableInsertion(
                $"{openingLine}\n{{{{ end }}}}",
                openingLine.Length
            ),
            loopStatement,
            children
        );
    }
}
