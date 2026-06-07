using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class EditCollectionUploadSlotLinkCryptersDialog(
    IUploadConfigLinkCrypterReadRepository linkCrypterReadRepository
) : OwningComponentBase
{
    [Parameter]
    public int CollectionUploadSlotId { get; set; }

    [Parameter]
    public string SlotName { get; set; } = string.Empty;

    [Parameter]
    public IReadOnlyList<CollectionUploadSlotLinkCrypterReadModel> SharedLinkCrypters { get; set; } =
        [];

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private readonly List<LinkCrypterOptionFormModel> linkCrypterOptions = [];
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        var selectedIds = SharedLinkCrypters
            .Select(linkCrypter => linkCrypter.LinkCrypterRegistrationId)
            .ToHashSet();
        var activeOptions = await linkCrypterReadRepository.GetLinkCrypterOptionsAsync();

        linkCrypterOptions.AddRange(
            activeOptions
                .OrderBy(option => option.Name)
                .Select(option => new LinkCrypterOptionFormModel(
                    option.LinkCrypterRegistrationId,
                    option.Name,
                    selectedIds.Contains(option.LinkCrypterRegistrationId)
                ))
        );

        var activeIds = activeOptions
            .Select(option => option.LinkCrypterRegistrationId)
            .ToHashSet();
        linkCrypterOptions.AddRange(
            SharedLinkCrypters
                .Where(linkCrypter => !activeIds.Contains(linkCrypter.LinkCrypterRegistrationId))
                .OrderBy(linkCrypter => linkCrypter.LinkCrypterRegistrationName)
                .Select(linkCrypter => new LinkCrypterOptionFormModel(
                    linkCrypter.LinkCrypterRegistrationId,
                    linkCrypter.LinkCrypterRegistrationName,
                    true
                ))
        );

        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var selectedIds = linkCrypterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.LinkCrypterRegistrationId)
            .ToList();

        var service = ScopedServices.GetRequiredService<ReleaseCollectionService>();
        await service.UpdateSharedLinkCryptersAsync(CollectionUploadSlotId, selectedIds);
        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private sealed class LinkCrypterOptionFormModel(
        int linkCrypterRegistrationId,
        string name,
        bool isSelected
    )
    {
        public int LinkCrypterRegistrationId { get; } = linkCrypterRegistrationId;

        public string Name { get; } = name;

        public bool IsSelected { get; set; } = isSelected;
    }
}
