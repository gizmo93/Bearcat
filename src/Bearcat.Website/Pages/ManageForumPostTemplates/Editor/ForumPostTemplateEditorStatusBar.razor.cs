using System.Globalization;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public partial class ForumPostTemplateEditorStatusBar(ClientPlatform clientPlatform) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ForumPostTemplateValidationResult ValidationResult { get; set; } = null!;

    [Parameter]
    public int CharacterCount { get; set; }

    [Parameter]
    [EditorRequired]
    public string CursorPositionElementId { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string CursorPositionFormat { get; set; } = null!;

    [Parameter]
    public TimeSpan? PreviewRenderDuration { get; set; }

    [Parameter]
    public EventCallback OnJumpToFirstError { get; set; }

    private string SaveShortcutKeys => clientPlatform.IsMac ? "⌘S" : "Ctrl S";

    private string InitialCursorPosition =>
        string.Format(CultureInfo.CurrentCulture, CursorPositionFormat, 1, 1);

    private string CharacterCountText =>
        CharacterCount == 1
            ? L["CharacterCountOne"]
            : L["CharacterCount", CharacterCount.ToString("N0", CultureInfo.CurrentCulture)];

    private string SyntaxErrorCountText =>
        ValidationResult.Errors.Count == 1
            ? L["SyntaxErrorCountOne"]
            : L["SyntaxErrorCount", ValidationResult.Errors.Count];

    private IReadOnlyList<string> ErrorMessages =>
        ValidationResult.Errors.Select(FormatError).ToList();

    private string FormatError(ForumPostTemplateError error)
    {
        return error.Line is null
            ? error.Message
            : L["ForumPostTemplateErrorWithPosition", error.Line, error.Column ?? 1, error.Message];
    }

    private string GetRenderDurationText(TimeSpan renderDuration)
    {
        var milliseconds = (long)Math.Round(renderDuration.TotalMilliseconds);
        return L[
            "PreviewRenderedInMilliseconds",
            milliseconds.ToString("N0", CultureInfo.CurrentCulture)
        ];
    }
}
