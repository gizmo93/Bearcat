using Bearcat.Domain.Shared.ForumPostingRules;
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
        ReleaseContentType contentType
    ) => localizer[$"ReleaseContentType.{contentType}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ReleaseResolution resolution
    ) => localizer[$"ReleaseResolution.{resolution}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ReleaseSource source
    ) => localizer[$"ReleaseSource.{source}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ReleasePlatform platform
    ) => localizer[$"ReleasePlatform.{platform}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ClassificationSource source
    ) => localizer[$"ClassificationSource.{source}"];

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
        RemoteSourceDownloadState state
    ) => localizer[$"RemoteSourceDownloadState.{state}"];

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
        LinkCrypterContainerScope scope
    ) => localizer[$"LinkCrypterContainerScope.{scope}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        NotificationSeverity severity
    ) => localizer[$"NotificationSeverity.{severity}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        NotificationKind kind
    ) => localizer[$"NotificationKind.{kind}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        QualityCheckRuleType ruleType
    ) => localizer[$"QualityCheckRuleType.{ruleType}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ReuploadTrigger trigger
    ) => localizer[$"ReuploadTrigger.{trigger}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        ForumPostPostMode postMode
    ) => localizer[$"ForumPostPostMode.{postMode}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        RuleConditionOperator conditionOperator
    ) => localizer[$"RuleConditionOperator.{conditionOperator}"];

    public static string Localize(
        this IStringLocalizer<UiResource> localizer,
        AdditionalArchiveContentType type
    ) => localizer[$"AdditionalArchiveContentType.{type}"];

    public static string LocalizeRuleField(
        this IStringLocalizer<UiResource> localizer,
        string fieldName
    ) => localizer[$"RuleField.{fieldName}"];

    public static string LocalizeDescription(
        this IStringLocalizer<UiResource> localizer,
        ReleaseType releaseType
    ) => localizer[$"ReleaseType.{releaseType}.Description"];
}
