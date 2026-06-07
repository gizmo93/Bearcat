using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateOrEditReleaseTemplateDialog(
    IReleaseGroupReadRepository releaseGroupReadRepository
) : OwningComponentBase
{
    [Parameter]
    public ReleaseTemplateFormModel FormModel { get; set; } = null!;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;

    private IEnumerable<SelectOption<int>> ReleaseGroupOptions =>
        releaseGroups.Select(group => new SelectOption<int>(group.ReleaseGroupId, group.Name));

    private IEnumerable<SelectOption<ReleaseType>> ReleaseTypeOptions =>
        Enum.GetValues<ReleaseType>()
            .Select(type => new SelectOption<ReleaseType>(type, L.Localize(type)));

    private IEnumerable<SelectOption<ReleaseCollectionDetectionMode>> ReleaseCollectionDetectionModeOptions =>
        new[]
        {
            ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            ReleaseCollectionDetectionMode.CustomRegex,
        }.Select(mode => new SelectOption<ReleaseCollectionDetectionMode>(mode, L.Localize(mode)));

    private string GetReleaseGroupDisplayText(int releaseGroupId)
    {
        return releaseGroups.FirstOrDefault(group => group.ReleaseGroupId == releaseGroupId)?.Name
            ?? releaseGroupId.ToString();
    }

    private string GetReleaseTypeDisplayText(ReleaseType releaseType)
    {
        return L.Localize(releaseType);
    }

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;

        releaseGroups = await releaseGroupReadRepository.GetAllAsync();

        if (releaseGroups.Count > 0 && FormModel.ReleaseGroupId == 0)
        {
            FormModel.ReleaseGroupId = releaseGroups[0].ReleaseGroupId;
        }
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();

        if (FormModel.IsEdit && FormModel.ReleaseTemplateId is not null)
        {
            await service.UpdateAsync(
                FormModel.ReleaseTemplateId.Value,
                FormModel.Name,
                FormModel.ReleaseType,
                FormModel.ReleaseGroupId,
                FormModel.UseReleaseCollections,
                FormModel.ReleaseCollectionDetectionMode,
                FormModel.ReleaseCollectionPattern,
                FormModel.ReleaseCollectionKeyTemplate,
                FormModel.ReleaseCollectionNameTemplate
            );
            await DialogRef.CloseAsync(DialogResult.Ok(FormModel.ReleaseTemplateId.Value));
            return;
        }

        var releaseTemplateId = await service.CreateAsync(
            FormModel.Name,
            FormModel.ReleaseType,
            FormModel.ReleaseGroupId,
            FormModel.UseReleaseCollections,
            FormModel.ReleaseCollectionDetectionMode,
            FormModel.ReleaseCollectionPattern,
            FormModel.ReleaseCollectionKeyTemplate,
            FormModel.ReleaseCollectionNameTemplate
        );
        await DialogRef.CloseAsync(DialogResult.Ok(releaseTemplateId));
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, L["NameIsRequired"]);
        }

        if (FormModel.ReleaseGroupId == 0)
        {
            messageStore.Add(() => FormModel.ReleaseGroupId, L["SelectReleaseGroupRequired"]);
        }

        if (
            FormModel.UseReleaseCollections
            && FormModel.ReleaseCollectionDetectionMode is ReleaseCollectionDetectionMode.CustomRegex
        )
        {
            if (string.IsNullOrWhiteSpace(FormModel.ReleaseCollectionPattern))
            {
                messageStore.Add(
                    () => FormModel.ReleaseCollectionPattern!,
                    L["ReleaseCollectionPatternRequired"]
                );
            }

            if (string.IsNullOrWhiteSpace(FormModel.ReleaseCollectionKeyTemplate))
            {
                messageStore.Add(
                    () => FormModel.ReleaseCollectionKeyTemplate!,
                    L["ReleaseCollectionKeyTemplateRequired"]
                );
            }

            if (string.IsNullOrWhiteSpace(FormModel.ReleaseCollectionNameTemplate))
            {
                messageStore.Add(
                    () => FormModel.ReleaseCollectionNameTemplate!,
                    L["ReleaseCollectionNameTemplateRequired"]
                );
            }
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
