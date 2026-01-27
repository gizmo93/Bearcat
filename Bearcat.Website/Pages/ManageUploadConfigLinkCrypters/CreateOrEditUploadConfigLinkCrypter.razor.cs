using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageUploadConfigLinkCrypters;

public partial class CreateOrEditUploadConfigLinkCrypter
{
    [Parameter]
    public int UploadConfigId { get; set; }

    [Parameter]
    public int? UploadConfigLinkCrypterId { get; set; }
    
    [Parameter]
    public string? ReleaseName { get; set; }

    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;
    
    private IReadOnlyDictionary<int, string> linkCrypterOptions = new Dictionary<int, string>();
    
    private bool isInitialized = false;
    
    private FormModel formModel = new();
    
    private EditContext editContext = null!;
    
    private ValidationMessageStore messageStore = null!;
    
    private IUploadConfigLinkCrypterReadRepository readRepository = null!;
    
    private bool IsEdit => UploadConfigLinkCrypterId.HasValue;


    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<IUploadConfigLinkCrypterReadRepository>();

        linkCrypterOptions = await readRepository.GetLinkCrypterOptionsAsync();
        await InitializeFormModelAsync();
        
        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        messageStore = new ValidationMessageStore(editContext);
        
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<UploadConfigLinkCrypterService>();
        if (IsEdit)
        {
            await service.UpdateAsync(
                UploadConfigLinkCrypterId!.Value,
                formModel.Password);
        }
        else
        {
            await service.CreateAsync(
                uploadConfigId: UploadConfigId,
                linkCrypterRegistrationId: formModel.LinkCrypterRegistrationId!.Value,
                password: formModel.Password);
        }
        
        MudDialog.Close();
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (formModel.LinkCrypterRegistrationId is null)
        {
            messageStore.Add(() => formModel.LinkCrypterRegistrationId!, "You need to select a Link Crypter.");
        }
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEdit)
        {
            formModel = new FormModel();
            
            return;
        }
        
        var configDto = await readRepository.GetByIdAsync(UploadConfigLinkCrypterId!.Value);
        
        formModel = new FormModel
        {
            LinkCrypterRegistrationId = configDto.LinkCrypterRegistrationId,
            Password = configDto.Password
        };
    }
}

