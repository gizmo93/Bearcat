using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public partial class ReleaseSearchFilters : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ReleaseSearchQuery Query { get; set; } = new();

    [Parameter]
    public IReadOnlyList<HosterRegistrationReadModel> HosterRegistrations { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ArchiverDto> Archivers { get; set; } = [];

    [Parameter]
    public IReadOnlyList<LinkCrypterRegistrationReadModel> LinkCrypterRegistrations { get; set; } =
    [];

    [Parameter]
    public IReadOnlyList<ReleaseGroupReadModel> ReleaseGroups { get; set; } = [];

    [Parameter]
    public EventCallback<ReleaseSearchQuery> OnQueryChanged { get; set; }

    private ReleaseSearchQuery EmptyQuery => new(SortOrder: Query.SortOrder);

    private bool HasActiveSearch => Query != EmptyQuery;

    private IReadOnlyList<SelectOption<ReleaseContentType?>> ReleaseContentTypeOptions =>
        Enum.GetValues<ReleaseContentType>()
            .Select(type => new SelectOption<ReleaseContentType?>(type, L.Localize(type)))
            .ToList();

    private IReadOnlyList<SelectOption<ReleaseType?>> ReleaseTypeOptions =>
        Enum.GetValues<ReleaseType>()
            .Select(type => new SelectOption<ReleaseType?>(type, L.Localize(type)))
            .ToList();

    private IReadOnlyList<SelectOption<string>> PrimaryLanguageOptions =>
        PrimaryLanguageSelectOptions.Create(L["NotSet"]);

    private IReadOnlyList<SelectOption<int?>> ReleaseGroupOptions =>
        ReleaseGroups
            .Select(group => new SelectOption<int?>(group.ReleaseGroupId, group.Name))
            .ToList();

    private IReadOnlyList<SelectOption<int?>> HosterRegistrationOptions =>
        HosterRegistrations
            .Select(hoster => new SelectOption<int?>(
                hoster.Id,
                $"{hoster.Name} ({hoster.HosterName})"
            ))
            .ToList();

    private IReadOnlyList<SelectOption<string>> ArchiverOptions =>
        Archivers
            .Select(archiver => new SelectOption<string>(
                archiver.ClassName,
                $"{archiver.Name} ({archiver.FileExtension})"
            ))
            .ToList();

    private IReadOnlyList<SelectOption<int?>> LinkCrypterRegistrationOptions =>
        LinkCrypterRegistrations
            .Select(linkCrypter => new SelectOption<int?>(
                linkCrypter.LinkCrypterRegistrationId,
                $"{linkCrypter.Name} ({linkCrypter.CrypterName})"
            ))
            .ToList();

    private Task ApplyAsync(ReleaseSearchQuery query) => OnQueryChanged.InvokeAsync(query);

    private Task ResetAllAsync() => OnQueryChanged.InvokeAsync(EmptyQuery);
}
