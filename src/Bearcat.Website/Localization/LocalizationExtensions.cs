using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Localization;

public static class LocalizationExtensions
{
    public static string Localize(this IStringLocalizer<UiResource> localizer, OnlineState state) =>
        localizer[$"OnlineState.{state}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ReleaseType releaseType
    ) => localizer[$"ReleaseType.{releaseType}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ReleaseCollectionDetectionMode mode
    ) => localizer[$"ReleaseCollectionDetectionMode.{mode}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        CollectionUploadSlotPasswordPolicy policy
    ) => localizer[$"CollectionUploadSlotPasswordPolicy.{policy}"];

    public static string Localize(this IStringLocalizer<UiResource> localizer, UploadState state) =>
        localizer[$"UploadState.{state}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ArchiveState state
    ) => localizer[$"ArchiveState.{state}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        LinkCrypterContainerState state
    ) => localizer[$"LinkCrypterContainerState.{state}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        NotificationType type
    ) => localizer[$"NotificationType.{type}"];

    public static string LocalizeDescription(
        this IStringLocalizer<UiResource> localizer,
        ReleaseType releaseType
    ) => localizer[$"ReleaseType.{releaseType}.Description"];
}
