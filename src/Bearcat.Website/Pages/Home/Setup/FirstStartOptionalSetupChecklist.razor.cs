using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Setup;

public partial class FirstStartOptionalSetupChecklist : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public FirstStartOptionalSetupStatus OptionalSetupStatus { get; set; } = null!;

    [Parameter]
    public EventCallback OnHide { get; set; }

    private List<ChecklistItem> GetItems()
    {
        return
        [
            new(
                "database",
                L["FirstStartOptionalSetupMetadata"],
                L["FirstStartOptionalSetupMetadataDescription"],
                OptionalSetupStatus.HasNfoDatabaseOrMediaDatabaseRegistration,
                [
                    new(L["NfoDatabases"], "/nfo-database-registrations"),
                    new(L["MediaDatabases"], "/media-database-registrations"),
                ]
            ),
            new(
                "image-up",
                L["FirstStartOptionalSetupImageHoster"],
                L["FirstStartOptionalSetupImageHosterDescription"],
                OptionalSetupStatus.HasImageHosterRegistration,
                [new(L["ImageHosters"], "/image-hoster-registrations")]
            ),
            new(
                "shield",
                L["FirstStartOptionalSetupLinkCrypters"],
                L["FirstStartOptionalSetupLinkCryptersDescription"],
                OptionalSetupStatus.HasLinkCrypterRegistration,
                [new(L["Crypters"], "/link-crypter-registrations")]
            ),
            new(
                "send",
                L["FirstStartOptionalSetupTelegram"],
                L["FirstStartOptionalSetupTelegramDescription"],
                OptionalSetupStatus.HasConnectedTelegramConfiguration,
                [new(L["FirstStartOptionalSetupTelegramLink"], "/telegram")]
            ),
            new(
                "message-square-text",
                L["FirstStartOptionalSetupForumPosting"],
                L["FirstStartOptionalSetupForumPostingDescription"],
                OptionalSetupStatus.HasDistributionSiteRegistration,
                [new(L["DistributionSites"], "/distribution-site-registrations")]
            ),
            new(
                "network",
                L["FirstStartOptionalSetupProxyServers"],
                L["FirstStartOptionalSetupProxyServersDescription"],
                OptionalSetupStatus.HasProxyServer,
                [new(L["ProxyServers"], "/proxy-servers")]
            ),
        ];
    }

    private sealed record ChecklistItem(
        string IconName,
        string Title,
        string Description,
        bool IsDone,
        List<ChecklistLink> Links
    );

    private sealed record ChecklistLink(string Label, string Href);
}
