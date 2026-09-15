using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared;

public static class ReleaseLanguageResolver
{
    public static string? Resolve(Release release)
    {
        var userLanguage = LanguageCatalog.Resolve(release.PrimaryLanguageCode);

        return userLanguage ?? release.Classification?.PrimaryLanguage;
    }
}
