using Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseGroups;

public partial class CreateOrEditReleaseGroupDialog : OwningComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public ReleaseGroupFormModel FormModel { get; set; } = new();

    [Parameter]
    public IReadOnlyList<QualityProfileReadModel> QualityProfiles { get; set; } = [];

    private EditContext editContext = null!;
    private ValidationMessageStore? messageStore;

    private IEnumerable<SelectOption<int?>> QualityProfileOptions =>
        [
            new SelectOption<int?>(null, L["NoQualityProfile"]),
            .. QualityProfiles.Select(profile => new SelectOption<int?>(profile.Id, profile.Name)),
        ];

    protected override void OnInitialized()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseGroupService>();

        if (FormModel is { IsEdit: true, ReleaseGroupId: not null })
        {
            await service.UpdateAsync(
                FormModel.ReleaseGroupId.Value,
                FormModel.Name,
                FormModel.EnableAutomaticReuploads,
                FormModel.NumberOfHoursUntilReupload,
                FormModel.QualityProfileId
            );

            await DialogRef.CloseAsync(DialogResult.Ok(FormModel.ReleaseGroupId.Value));
            return;
        }

        var id = await service.CreateAsync(
            FormModel.Name,
            FormModel.EnableAutomaticReuploads,
            FormModel.NumberOfHoursUntilReupload,
            FormModel.QualityProfileId
        );

        await DialogRef.CloseAsync(DialogResult.Ok(id));
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, L["NameIsRequired"]);
        }

        if (FormModel.NumberOfHoursUntilReupload < 0)
        {
            messageStore.Add(
                () => FormModel.NumberOfHoursUntilReupload,
                L["HoursUntilReuploadMustBeZeroOrGreater"]
            );
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
