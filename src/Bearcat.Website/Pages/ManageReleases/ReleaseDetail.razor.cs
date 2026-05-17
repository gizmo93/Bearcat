using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseDetail(NavigationManager navigationManager, DialogService dialogService)
    : OwningComponentBase
{
    [Parameter]
    public int ReleaseId { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [SupplyParameterFromQuery(Name = "uploadConfigId")]
    public int? FocusUploadConfigId { get; set; }

    [SupplyParameterFromQuery(Name = "archiveConfigId")]
    public int? FocusArchiveConfigId { get; set; }

    private IReleaseReadRepository releaseReadRepository = null!;
    private ReleaseDto release = null!;
    private bool isInitialized;
    private string? activeTab = "archives";
    private readonly Dictionary<string, IReloadableComponent> reloadableComponents = new();

    protected override void OnParametersSet()
    {
        activeTab = NormalizeTab(RequestedTab);
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        releaseReadRepository = ScopedServices.GetRequiredService<IReleaseReadRepository>();

        var releaseDto = await releaseReadRepository.GetReleaseAsync(ReleaseId);

        if (releaseDto is null)
        {
            navigationManager.NotFound();
            return;
        }

        release = releaseDto;
        isInitialized = true;
    }

    private async Task HandleChangeAffectingOtherComponentsAsync(string componentName)
    {
        var affectedComponents = reloadableComponents
            .Where(c => c.Key != componentName)
            .Select(c => c.Value);

        foreach (var component in affectedComponents)
        {
            await component.ReloadAsync();
        }
    }

    private Task HandleTabChangedAsync(string? value)
    {
        activeTab = NormalizeTab(value);
        return Task.CompletedTask;
    }

    private static string NormalizeTab(string? tab) =>
        tab switch
        {
            "upload-configs" => "upload-configs",
            "uploads" => "uploads",
            _ => "archives",
        };

    private async Task ShowEditReleaseDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseDialog.ReleaseId)] = release.ReleaseId,
            [nameof(CreateOrEditReleaseDialog.FormModel)] = new ReleaseFormModel
            {
                Name = release.Name,
                ReleaseGroupId = release.ReleaseGroupId,
                IsEdit = true,
            },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", release.Name],
                Description = L["EditReleaseDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await ReloadReleaseAsync();
        }
    }

    private async Task DeleteReleaseAsync()
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteReleaseTitle", release.Name],
            L["DeleteReleaseConfirmation", release.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<ReleaseService>();
        await service.DeleteAsync(release.ReleaseId);
        navigationManager.NavigateTo("/releases");
    }

    private async Task ReloadReleaseAsync()
    {
        var releaseDto = await releaseReadRepository.GetReleaseAsync(ReleaseId);

        if (releaseDto is null)
        {
            navigationManager.NotFound();
            return;
        }

        release = releaseDto;
    }
}
