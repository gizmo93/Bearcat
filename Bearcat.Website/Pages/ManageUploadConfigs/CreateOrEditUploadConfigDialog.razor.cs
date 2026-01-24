using BearCat.Core.Domain.UseCases.ManageUploadConfigs;
using BearCat.Core.Domain.UseCases.ManageUploadConfigs.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageUploadConfigs;

public partial class CreateOrEditUploadConfigDialog
{
    [Parameter]
    public int ReleaseId { get; set; }
    
    [Parameter]
    public int? UploadConfigId { get; set; }
    
    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;
    
    private bool IsEdit => UploadConfigId.HasValue;
    
    private IUploadConfigReadRepository readRepository = null!;

    private UploadConfigFormModel formModel = null!;
    
    private EditContext editContext = null!;
    
    private ValidationMessageStore messageStore = null!;
    
    private IReadOnlyDictionary<int, string> hosterRegistrationOptions = null!;
    
    private IReadOnlyDictionary<int, string> archiveConfigOptions = null!;

    private bool isInitialized;
    
    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<IUploadConfigReadRepository>();
        
        await InitializeFormModelAsync();
        hosterRegistrationOptions = await readRepository.GetHosterRegistrationOptionsAsync();
        archiveConfigOptions = await readRepository.GetArchiveConfigOptionsAsync(ReleaseId);
        
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<UploadConfigService>();

        if (IsEdit)
        {
            await service.UpdateAsync(
                uploadConfigId: UploadConfigId!.Value,
                name: formModel.Name!,
                hosterRegistrationId: formModel.HosterRegistrationId!.Value,
                archiveConfigId: formModel.ArchiveConfigId!.Value,
                linksDistributedTo: formModel.LinksDistributedTo);
        }
        else
        {
            await service.CreateAsync(
                releaseId: ReleaseId,
                name: formModel.Name!,
                hosterRegistrationId: formModel.HosterRegistrationId!.Value,
                archiveConfigId: formModel.ArchiveConfigId!.Value,
                linksDistributedTo: formModel.LinksDistributedTo);
        }
        
        MudDialog.Close();
    }
    
    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();
        
        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            messageStore.Add(
                () => formModel.Name!,
                "Name is required.");
        }
        
        if (formModel.HosterRegistrationId is null)
        {
            messageStore.Add(
                () => formModel.HosterRegistrationId!,
                "Hoster Registration is required.");
        }
        
        if (formModel.ArchiveConfigId is null)
        {
            messageStore.Add(
                () => formModel.ArchiveConfigId!,
                "Archive Config is required.");
        }
    }
    
    private void DeleteLinkDistributedTo(int index)
    {
        formModel.LinksDistributedTo.RemoveAt(index);
    }
    
    private void AddLinkDistributedTo()
    {
        formModel.LinksDistributedTo.Add(string.Empty);
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEdit)
        {
            formModel = new UploadConfigFormModel();

            return;
        }
        
        var uploadConfig = await readRepository.GetDtoByIdAsync(UploadConfigId!.Value);

        formModel = new UploadConfigFormModel
        {
            Name = uploadConfig.Name,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            ArchiveConfigId = uploadConfig.ArchiveConfigId,
            LinksDistributedTo = uploadConfig.LinksDistributedTo.ToList()
        };
    }
}

