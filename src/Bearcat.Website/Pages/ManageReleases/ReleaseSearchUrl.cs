using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.ValueObjects;
using Microsoft.AspNetCore.WebUtilities;

namespace Bearcat.Website.Pages.ManageReleases;

public static class ReleaseSearchUrl
{
    public const int DefaultPageSize = 5;
    public static readonly IReadOnlyList<int> PageSizes = [5, 10, 20, 50, 100];

    private const string BasePath = "/releases";
    private const string LanguageNotSetValue = "none";

    public static ReleaseSearchUrlState Parse(IReleaseSearchUrlValues values)
    {
        var query = new ReleaseSearchQuery(
            SearchTerm: NormalizeText(values.SearchTerm),
            ReleaseType: ParseEnum<ReleaseType>(values.ReleaseType),
            ReleaseContentType: ParseEnum<ReleaseContentType>(values.ReleaseContentType),
            PrimaryLanguageCode: NormalizeText(values.Language) switch
            {
                null => null,
                LanguageNotSetValue => string.Empty,
                var code => code,
            },
            OnlineState: ParseEnum<OnlineState>(values.OnlineState),
            HosterRegistrationId: values.HosterRegistrationId,
            ArchiverName: NormalizeText(values.ArchiverName),
            LinkCrypterRegistrationId: values.LinkCrypterRegistrationId,
            ReleaseGroupId: values.ReleaseGroupId == 0 ? null : values.ReleaseGroupId,
            PostedLocationUrl: NormalizeText(values.PostedLocationUrl),
            DownloadLink: NormalizeText(values.DownloadLink),
            ArchiveFileName: NormalizeText(values.ArchiveFileName),
            UploadId: NormalizeText(values.UploadId)
        );

        var pageIndex = Math.Max(0, (values.Page ?? 1) - 1);
        var pageSize =
            values.PageSize is int size && PageSizes.Contains(size) ? size : DefaultPageSize;

        return new ReleaseSearchUrlState(query, pageIndex, pageSize);
    }

    public static string Build(ReleaseSearchQuery query, int page, int pageSize)
    {
        var parameters = new (string Key, string? Value)[]
        {
            ("q", NormalizeText(query.SearchTerm)),
            ("type", query.ReleaseType?.ToString()),
            ("content", query.ReleaseContentType?.ToString()),
            (
                "lang",
                query.PrimaryLanguageCode switch
                {
                    "" => LanguageNotSetValue,
                    var code => code,
                }
            ),
            ("state", query.OnlineState?.ToString()),
            ("hoster", query.HosterRegistrationId?.ToString()),
            ("archiver", NormalizeText(query.ArchiverName)),
            ("crypter", query.LinkCrypterRegistrationId?.ToString()),
            ("group", query.ReleaseGroupId?.ToString()),
            ("posted", NormalizeText(query.PostedLocationUrl)),
            ("link", NormalizeText(query.DownloadLink)),
            ("file", NormalizeText(query.ArchiveFileName)),
            ("upload", NormalizeText(query.UploadId)),
            ("page", page > 1 ? page.ToString() : null),
            ("size", pageSize == DefaultPageSize ? null : pageSize.ToString()),
        };

        var activeParameters = parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => KeyValuePair.Create(parameter.Key, parameter.Value));

        return QueryHelpers.AddQueryString(BasePath, activeParameters);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;
    }
}
