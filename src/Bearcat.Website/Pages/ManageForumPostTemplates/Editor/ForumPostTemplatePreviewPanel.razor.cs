using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;
using Bearcat.Domain.ValueObjects;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public partial class ForumPostTemplatePreviewPanel : ComponentBase
{
    private const string OutputTab = "output";
    private const string DataTab = "data";

    private string? activeTab = OutputTab;

    [Parameter]
    public ForumPostTemplateType Type { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public bool HasPreviewEntity { get; set; }

    [Parameter]
    public string? Content { get; set; }

    [Parameter]
    public IReadOnlyList<ForumPostTemplateError> Errors { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ForumPostTemplateDataNode> DataNodes { get; set; } = [];

    [Parameter]
    public EventCallback<ForumPostTemplateError> OnErrorPositionSelected { get; set; }

    private string OutputClass =>
        Errors.Count > 0
            ? "bearcat-forum-template-editor-preview-output bearcat-forum-template-editor-preview-output-outdated"
            : "bearcat-forum-template-editor-preview-output";

    private string NoPreviewEntityText =>
        Type switch
        {
            ForumPostTemplateType.ReleaseCollection => L[
                "ForumPostTemplatePreviewNoReleaseCollection"
            ],
            _ => L["ForumPostTemplatePreviewNoRelease"],
        };
}
