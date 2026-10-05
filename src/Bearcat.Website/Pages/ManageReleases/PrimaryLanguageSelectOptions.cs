using System.Globalization;
using BlazorBlueprint.Primitives;

namespace Bearcat.Website.Pages.ManageReleases;

public static class PrimaryLanguageSelectOptions
{
    private static readonly IReadOnlyList<SelectOption<string>> LanguageOptions = CultureInfo
        .GetCultures(CultureTypes.NeutralCultures)
        .Where(culture => culture.TwoLetterISOLanguageName.Length == 2)
        .DistinctBy(culture => culture.TwoLetterISOLanguageName)
        .OrderBy(culture => culture.NativeName)
        .Select(culture => new SelectOption<string>(
            culture.TwoLetterISOLanguageName,
            culture.NativeName
        ))
        .ToList();

    public static IReadOnlyList<SelectOption<string>> Create(string notSetText) =>
        [new(string.Empty, notSetText), .. LanguageOptions];
}
