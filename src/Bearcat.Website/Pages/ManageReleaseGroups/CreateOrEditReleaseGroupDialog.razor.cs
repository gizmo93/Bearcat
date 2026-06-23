using Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;
using Bearcat.Domain.UseCases.ManageQualityProfiles.Repositories;
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

    private EditContext editContext = null!;
    private ValidationMessageStore? messageStore;
    private IReadOnlyList<QualityProfileReadModel> qualityProfiles = [];

    private IEnumerable<SelectOption<int?>> QualityProfileOptions =>
        qualityProfiles.Select(profile => new SelectOption<int?>(profile.Id, profile.Name));

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;

        qualityProfiles = await ScopedServices
            .GetRequiredService<IQualityProfileReadRepository>()
            .GetAllAsync();
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
