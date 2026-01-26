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
                formModel.ContainerName,
                formModel.Password);
        }
        else
        {
            await service.CreateAsync(
                uploadConfigId: UploadConfigId,
                linkCrypterRegistrationId: formModel.LinkCrypterRegistrationId,
                containerName: formModel.ContainerName,
                password: formModel.Password);
        }
        
        MudDialog.Close();
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.ContainerName))
        {
            messageStore.Add(() => formModel.ContainerName, "Container Name is required.");
        }

        if (formModel.LinkCrypterRegistrationId <= 0)
        {
            messageStore.Add(() => formModel.LinkCrypterRegistrationId, "You need to select a Link Crypter.");
        }
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEdit)
        {
            formModel = new FormModel
            {
                ContainerName = ReleaseName ?? string.Empty,
            };
            
            return;
        }
        
        var configDto = await readRepository.GetByIdAsync(UploadConfigLinkCrypterId!.Value);
        
        formModel = new FormModel
        {
            LinkCrypterRegistrationId = configDto.LinkCrypterRegistrationId,
            ContainerName = configDto.ContainerName,
            Password = configDto.Password
        };
    }
}

