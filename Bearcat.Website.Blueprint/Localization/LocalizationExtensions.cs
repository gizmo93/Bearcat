using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Blueprint.Localization;

public static class LocalizationExtensions
{
    public static string Localize(this IStringLocalizer<UiResource> localizer, OnlineState state) =>
        localizer[$"OnlineState.{state}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ReleaseType releaseType
    ) => localizer[$"ReleaseType.{releaseType}"];

    public static string LocalizeDescription(
        this IStringLocalizer<UiResource> localizer,
        ReleaseType releaseType
    ) => localizer[$"ReleaseType.{releaseType}.Description"];

    public static string Localize(this IStringLocalizer<UiResource> localizer, UploadState state) =>
        localizer[$"UploadState.{state}"];
}
