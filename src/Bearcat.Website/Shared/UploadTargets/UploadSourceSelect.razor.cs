using System.Linq.Expressions;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared.UploadTargets;

public partial class UploadSourceSelect : ComponentBase
{
    [Parameter]
    public UploadSource Value { get; set; }

    [Parameter]
    public EventCallback<UploadSource> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<UploadSource>>? ValueExpression { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    private IReadOnlyList<SelectOption<UploadSource>> Options =>
        Enum.GetValues<UploadSource>()
            .Select(uploadSource => new SelectOption<UploadSource>(
                uploadSource,
                L.Localize(uploadSource)
            ))
            .ToList();

    private string GetDisplayText(UploadSource uploadSource)
    {
        return L.Localize(uploadSource);
    }
}
