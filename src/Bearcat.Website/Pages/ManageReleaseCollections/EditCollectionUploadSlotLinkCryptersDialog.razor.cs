using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class EditCollectionUploadSlotLinkCryptersDialog(
    ILinkCrypterRegistrationReadRepository linkCrypterRegistrationReadRepository
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
        var selectedLinkCryptersByRegistrationId = SharedLinkCrypters.ToDictionary(linkCrypter =>
            linkCrypter.LinkCrypterRegistrationId
        );
        var registrations = await linkCrypterRegistrationReadRepository.GetAllAsync();

        linkCrypterOptions.AddRange(
            registrations
                .Where(registration =>
                    registration.IsActive
                    || selectedLinkCryptersByRegistrationId.ContainsKey(
                        registration.LinkCrypterRegistrationId
                    )
                )
                .OrderBy(registration => registration.Name)
                .Select(registration =>
                {
                    selectedLinkCryptersByRegistrationId.TryGetValue(
                        registration.LinkCrypterRegistrationId,
                        out var selectedLinkCrypter
                    );

                    return new LinkCrypterOptionFormModel(
                        registration.LinkCrypterRegistrationId,
                        registration.Name,
                        registration.SupportsCaptcha,
                        registration.SupportsContainerDownload,
                        registration.SupportsClickAndLoad,
                        selectedLinkCrypter
                    );
                })
        );

        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var settings = linkCrypterOptions
            .Where(option => option.IsSelected)
            .Select(option => new CollectionUploadSlotLinkCrypterSettings(
                option.LinkCrypterRegistrationId,
                option.Password,
                option.SupportsCaptcha && option.EnableCaptcha,
                option.SupportsContainerDownload && option.EnableContainerDownload,
                option.SupportsClickAndLoad && option.EnableClickAndLoad
            ))
            .ToList();

        var service = ScopedServices.GetRequiredService<ReleaseCollectionService>();
        await service.UpdateSharedLinkCryptersAsync(CollectionUploadSlotId, settings);
        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private sealed class LinkCrypterOptionFormModel(
        int linkCrypterRegistrationId,
        string name,
        bool supportsCaptcha,
        bool supportsContainerDownload,
        bool supportsClickAndLoad,
        CollectionUploadSlotLinkCrypterReadModel? selectedLinkCrypter
    )
    {
        public int LinkCrypterRegistrationId { get; } = linkCrypterRegistrationId;

        public string Name { get; } = name;

        public bool SupportsCaptcha { get; } = supportsCaptcha;

        public bool SupportsContainerDownload { get; } = supportsContainerDownload;

        public bool SupportsClickAndLoad { get; } = supportsClickAndLoad;

        public bool IsSelected { get; set; } = selectedLinkCrypter is not null;

        public string? Password { get; set; } = selectedLinkCrypter?.Password;

        public bool EnableCaptcha { get; set; } =
            selectedLinkCrypter?.EnableCaptcha ?? supportsCaptcha;

        public bool EnableContainerDownload { get; set; } =
            selectedLinkCrypter?.EnableContainerDownload ?? supportsContainerDownload;

        public bool EnableClickAndLoad { get; set; } =
            selectedLinkCrypter?.EnableClickAndLoad ?? supportsClickAndLoad;
    }
}
