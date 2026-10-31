using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Shared;

public static class LinkCrypterContainerActionToasts
{
    public static void ShowForceUpdateResult(
        ToastService toastService,
        IStringLocalizer<UiResource> localizer,
        string previousContainerUrl,
        LinkCrypterContainer container
    )
    {
        var errors = string.Join("; ", container.Errors);

        switch (container.State)
        {
            case LinkCrypterContainerState.Created
                when !string.Equals(
                    container.ContainerUrl,
                    previousContainerUrl,
                    StringComparison.Ordinal
                ):
                toastService.Info(
                    localizer[
                        "LinkCrypterContainerNoLongerExistedAndWasRecreated",
                        container.ContainerUrl
                    ]
                );
                break;
            case LinkCrypterContainerState.Created:
                toastService.Success(localizer["LinkCrypterContainerUpdated"]);
                break;
            case LinkCrypterContainerState.UpdateFailed:
                toastService.Error(localizer["LinkCrypterContainerUpdateFailedWithErrors", errors]);
                break;
            case LinkCrypterContainerState.CreationFailed:
                toastService.Error(
                    localizer["LinkCrypterContainerCreationFailedWithErrors", errors]
                );
                break;
        }
    }

    public static void ShowRecreateResult(
        ToastService toastService,
        IStringLocalizer<UiResource> localizer,
        LinkCrypterContainer container
    )
    {
        if (container.State == LinkCrypterContainerState.Created)
        {
            toastService.Success(
                localizer["LinkCrypterContainerRecreatedWithUrl", container.ContainerUrl]
            );
            return;
        }

        toastService.Error(
            localizer["LinkCrypterContainerRecreationFailed", string.Join("; ", container.Errors)]
        );
    }
}
