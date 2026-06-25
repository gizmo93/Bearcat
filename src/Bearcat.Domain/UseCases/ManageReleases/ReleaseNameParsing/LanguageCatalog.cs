using System.Globalization;

namespace Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;

public static class LanguageCatalog
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ger"] = "German",
        ["deu"] = "German",
        ["deutsch"] = "German",
        ["eng"] = "English",
        ["englisch"] = "English",
        ["fre"] = "French",
        ["fra"] = "French",
        ["francais"] = "French",
        ["spa"] = "Spanish",
        ["espanol"] = "Spanish",
        ["ita"] = "Italian",
        ["jpn"] = "Japanese",
        ["jap"] = "Japanese",
        ["kor"] = "Korean",
        ["rus"] = "Russian",
        ["dut"] = "Dutch",
        ["nld"] = "Dutch",
    };

    private static readonly Dictionary<string, string> ByName = BuildNameLookup();

    private static readonly Dictionary<string, string> ByCode = BuildCodeLookup();

    public static bool TryResolveName(string token, out string canonical)
    {
        return ByName.TryGetValue(token, out canonical!);
    }

    public static string? Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (ByCode.TryGetValue(trimmed, out var byCode))
        {
            return byCode;
        }

        if (ByName.TryGetValue(trimmed, out var byName))
        {
            return byName;
        }

        var separatorIndex = trimmed.IndexOfAny(['-', '_']);

        return separatorIndex > 0 ? Resolve(trimmed[..separatorIndex]) : null;
    }

    private static Dictionary<string, string> BuildNameLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            var canonical = culture.EnglishName;

            if (canonical.Length < 4 || canonical.Contains('('))
            {
                continue;
            }

            AddName(lookup, canonical, canonical);
            AddName(lookup, culture.NativeName, canonical);
        }

        foreach (var alias in Aliases)
        {
            lookup[alias.Key] = alias.Value;
        }

        return lookup;
    }

    private static Dictionary<string, string> BuildCodeLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            var canonical = culture.EnglishName;

            if (canonical.Length < 4 || canonical.Contains('('))
            {
                continue;
            }

            AddCode(lookup, culture.TwoLetterISOLanguageName, canonical);
            AddCode(lookup, culture.ThreeLetterISOLanguageName, canonical);
        }

        foreach (var alias in Aliases)
        {
            lookup.TryAdd(alias.Key, alias.Value);
        }

        return lookup;
    }

    private static void AddName(Dictionary<string, string> lookup, string name, string canonical)
    {
        if (name.Length >= 4 && name.All(char.IsLetter))
        {
            lookup.TryAdd(name, canonical);
        }
    }

    private static void AddCode(Dictionary<string, string> lookup, string code, string canonical)
    {
        if (code.Length is 2 or 3 && code.All(char.IsLetter))
        {
            lookup.TryAdd(code, canonical);
        }
    }
}
