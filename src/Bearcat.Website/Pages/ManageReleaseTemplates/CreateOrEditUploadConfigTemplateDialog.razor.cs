using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateOrEditUploadConfigTemplateDialog(
    IHosterConfigurationReadRepository hosterReadRepository,
    IReleaseTemplateReadRepository releaseTemplateReadRepository
) : OwningComponentBase
{
    [Parameter]
    public UploadConfigTemplateFormModel FormModel { get; set; } = null!;

    [Parameter]
    public int ReleaseTemplateId { get; set; }

    [Parameter]
    public int? UploadConfigTemplateId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations = [];
    private IReadOnlyList<ArchiveConfigTemplateReadModel> archiveConfigTemplates = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isInitialized;
    private bool isEdit;

    private IEnumerable<SelectOption<int?>> HosterOptions =>
        hosterRegistrations
            .OrderBy(hoster => hoster.Name)
            .Select(hoster => new SelectOption<int?>(hoster.Id, hoster.Name));

    private IEnumerable<SelectOption<int?>> ArchiveConfigOptions =>
        archiveConfigTemplates
            .OrderBy(config => config.Name)
            .Select(config => new SelectOption<int?>(config.ArchiveConfigTemplateId, config.Name));

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isEdit = UploadConfigTemplateId is not null;

        hosterRegistrations = await hosterReadRepository.GetAllRegistrationsAsync();
        var detail = await releaseTemplateReadRepository.GetDetailAsync(ReleaseTemplateId);
        archiveConfigTemplates = detail?.ArchiveConfigTemplates ?? [];

        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();

        if (isEdit)
        {
            await service.UpdateUploadConfigTemplateAsync(
                UploadConfigTemplateId!.Value,
                FormModel.Name,
                FormModel.HosterRegistrationId!.Value,
                FormModel.ArchiveConfigTemplateId!.Value,
                FormModel.LinksDistributedTo
            );
        }
        else
        {
            await service.CreateUploadConfigTemplateAsync(
                ReleaseTemplateId,
                FormModel.Name,
                FormModel.HosterRegistrationId!.Value,
                FormModel.ArchiveConfigTemplateId!.Value,
                FormModel.LinksDistributedTo
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void AddLinkDistributedTo()
    {
        FormModel.LinksDistributedTo.Add(string.Empty);
    }

    private void DeleteLinkDistributedTo(int index)
    {
        FormModel.LinksDistributedTo.RemoveAt(index);
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (FormModel.HosterRegistrationId is null)
        {
            messageStore.Add(
                () => FormModel.HosterRegistrationId!,
                L["SelectHosterRegistrationRequired"]
            );
        }

        if (FormModel.ArchiveConfigTemplateId is null)
        {
            messageStore.Add(
                () => FormModel.ArchiveConfigTemplateId!,
                L["SelectArchiveConfigurationRequired"]
            );
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
