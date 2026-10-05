using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace Bearcat.Website.Shared;

public static class SearchUrlParameters
{
    public const int DefaultPageSize = 5;
    public static readonly IReadOnlyList<int> PageSizes = [5, 10, 20, 50, 100];

    public static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        return
            Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
    }

    public static int? NullWhenZero(int? id)
    {
        return id == 0 ? null : id;
    }

    public static int ParsePageIndex(int? page)
    {
        return Math.Max(0, (page ?? 1) - 1);
    }

    public static int ParsePageSize(int? pageSize)
    {
        return pageSize is int size && PageSizes.Contains(size) ? size : DefaultPageSize;
    }

    public static string Build(
        string basePath,
        IReadOnlyList<(string Key, string? Value)> filterParameters,
        int page,
        int pageSize
    )
    {
        List<(string Key, string? Value)> parameters =
        [
            .. filterParameters,
            ("page", page > 1 ? page.ToString(CultureInfo.InvariantCulture) : null),
            (
                "size",
                pageSize == DefaultPageSize ? null : pageSize.ToString(CultureInfo.InvariantCulture)
            ),
        ];

        var activeParameters = parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => KeyValuePair.Create(parameter.Key, parameter.Value));

        return QueryHelpers.AddQueryString(basePath, activeParameters);
    }
}
