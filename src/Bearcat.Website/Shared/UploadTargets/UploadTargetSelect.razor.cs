using System.Linq.Expressions;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared.UploadTargets;

public partial class UploadTargetSelect : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public UploadTargetChoices Choices { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public UploadSource UploadSource { get; set; }

    [Parameter]
    public UploadTarget? Value { get; set; }

    [Parameter]
    public EventCallback<UploadTarget?> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<UploadTarget?>>? ValueExpression { get; set; }
}
