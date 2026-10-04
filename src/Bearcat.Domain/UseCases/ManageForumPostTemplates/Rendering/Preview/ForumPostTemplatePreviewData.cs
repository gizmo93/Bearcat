using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;

public class ForumPostTemplatePreviewData
{
    internal ForumPostTemplatePreviewData(
        ScriptObject globals,
        IReadOnlyList<ForumPostTemplateDataNode> dataNodes
    )
    {
        Globals = globals;
        DataNodes = dataNodes;
    }

    internal ScriptObject Globals { get; }

    public IReadOnlyList<ForumPostTemplateDataNode> DataNodes { get; }
}
