using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class RuleFieldCatalog
{
    private static readonly IReadOnlyList<RuleConditionOperator> ReleaseNameOperators =
    [
        RuleConditionOperator.Equals,
        RuleConditionOperator.NotEquals,
        RuleConditionOperator.Like,
        RuleConditionOperator.NotLike,
        RuleConditionOperator.Regex,
    ];

    private static readonly IReadOnlyList<RuleConditionOperator> TextOperators =
    [
        RuleConditionOperator.Equals,
        RuleConditionOperator.NotEquals,
        RuleConditionOperator.In,
        RuleConditionOperator.NotIn,
        RuleConditionOperator.Like,
        RuleConditionOperator.NotLike,
        RuleConditionOperator.IsSet,
        RuleConditionOperator.IsNotSet,
    ];

    private static readonly IReadOnlyList<RuleConditionOperator> LanguageOperators =
    [
        RuleConditionOperator.Equals,
        RuleConditionOperator.NotEquals,
        RuleConditionOperator.In,
        RuleConditionOperator.NotIn,
        RuleConditionOperator.IsSet,
        RuleConditionOperator.IsNotSet,
    ];

    private static readonly IReadOnlyList<RuleConditionOperator> EnumerationOperators =
    [
        RuleConditionOperator.Equals,
        RuleConditionOperator.NotEquals,
        RuleConditionOperator.In,
        RuleConditionOperator.NotIn,
    ];

    private static readonly IReadOnlyList<RuleConditionOperator> ResolutionOperators =
    [
        RuleConditionOperator.Equals,
        RuleConditionOperator.NotEquals,
        RuleConditionOperator.In,
        RuleConditionOperator.NotIn,
        RuleConditionOperator.GreaterOrEqual,
        RuleConditionOperator.LessOrEqual,
    ];

    private static readonly IReadOnlyList<RuleConditionOperator> NumberOperators =
    [
        RuleConditionOperator.Equals,
        RuleConditionOperator.NotEquals,
        RuleConditionOperator.GreaterOrEqual,
        RuleConditionOperator.LessOrEqual,
        RuleConditionOperator.IsSet,
        RuleConditionOperator.IsNotSet,
    ];

    private static readonly IReadOnlyList<RuleFieldDescriptor> AllFields =
    [
        new RuleFieldDescriptor(
            Name: "ReleaseName",
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators: ReleaseNameOperators,
            Options: [],
            ValueAccessor: context => context.ReleaseName
        ),
        new RuleFieldDescriptor(
            Name: "Resolution",
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators: ResolutionOperators,
            Options: Enum.GetNames<ReleaseResolution>(),
            ValueAccessor: context => context.Resolution
        ),
        new RuleFieldDescriptor(
            Name: "PrimaryLanguage",
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators: LanguageOperators,
            Options: [],
            ValueAccessor: context => context.PrimaryLanguage
        ),
        new RuleFieldDescriptor(
            Name: "IsMultiLanguage",
            ValueKind: RuleFieldValueKind.Boolean,
            AllowedOperators: [RuleConditionOperator.Equals],
            Options: [],
            ValueAccessor: context => context.IsMultiLanguage
        ),
        new RuleFieldDescriptor(
            Name: "ContentType",
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators: EnumerationOperators,
            Options: Enum.GetNames<ReleaseContentType>(),
            ValueAccessor: context => context.ContentType
        ),
        new RuleFieldDescriptor(
            Name: "Platform",
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators: EnumerationOperators,
            Options: Enum.GetNames<ReleasePlatform>(),
            ValueAccessor: context => context.Platform
        ),
        new RuleFieldDescriptor(
            Name: "Source",
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators: EnumerationOperators,
            Options: Enum.GetNames<ReleaseSource>(),
            ValueAccessor: context => context.Source
        ),
        new RuleFieldDescriptor(
            Name: "ReleaseGroupName",
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators: TextOperators,
            Options: [],
            ValueAccessor: context => context.ReleaseGroupName
        ),
        new RuleFieldDescriptor(
            Name: "ReleaseGroupToken",
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators: TextOperators,
            Options: [],
            ValueAccessor: context => context.ReleaseGroupToken
        ),
        new RuleFieldDescriptor(
            Name: "Year",
            ValueKind: RuleFieldValueKind.Integer,
            AllowedOperators: NumberOperators,
            Options: [],
            ValueAccessor: context => context.Year
        ),
        new RuleFieldDescriptor(
            Name: "Season",
            ValueKind: RuleFieldValueKind.Integer,
            AllowedOperators: NumberOperators,
            Options: [],
            ValueAccessor: context => context.Season
        ),
        new RuleFieldDescriptor(
            Name: "Episode",
            ValueKind: RuleFieldValueKind.Integer,
            AllowedOperators: NumberOperators,
            Options: [],
            ValueAccessor: context => context.Episode,
            IsReleaseOnly: true
        ),
        new RuleFieldDescriptor(
            Name: "Genre",
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators: TextOperators,
            Options: [],
            ValueAccessor: context => context.Genre
        ),
    ];

    private static readonly Dictionary<string, RuleFieldDescriptor> FieldsByName =
        AllFields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<RuleFieldDescriptor> Fields => AllFields;

    public static IReadOnlyList<RuleFieldDescriptor> GetFields(ForumPostTemplateType entityType)
    {
        return AllFields
            .Where(field => entityType == ForumPostTemplateType.Release || !field.IsReleaseOnly)
            .ToList();
    }

    public static bool TryGet(string? name, out RuleFieldDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            descriptor = null!;

            return false;
        }

        return FieldsByName.TryGetValue(name, out descriptor!);
    }
}
