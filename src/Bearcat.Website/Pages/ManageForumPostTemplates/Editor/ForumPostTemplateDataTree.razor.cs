using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public partial class ForumPostTemplateDataTree : ComponentBase
{
    private IReadOnlyList<ForumPostTemplateDataNode>? renderedDataNodes;

    [Parameter]
    public IReadOnlyList<ForumPostTemplateDataNode> DataNodes { get; set; } = [];

    protected override bool ShouldRender()
    {
        if (ReferenceEquals(renderedDataNodes, DataNodes))
        {
            return false;
        }

        renderedDataNodes = DataNodes;
        return true;
    }

    protected override void OnInitialized()
    {
        renderedDataNodes = DataNodes;
    }
}
