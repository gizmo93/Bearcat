using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Pages.ManageForumPostTemplates.Editor;

public static class ForumPostTemplateTypeLabels
{
    public static string Get(IStringLocalizer<UiResource> localizer, ForumPostTemplateType type)
    {
        return type switch
        {
            ForumPostTemplateType.Release => localizer["ForumPostTemplateTypeRelease"],
            ForumPostTemplateType.ReleaseCollection => localizer[
                "ForumPostTemplateTypeReleaseCollection"
            ],
            _ => type.ToString(),
        };
    }
}
