using System.Globalization;
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
        CreateTextField("ReleaseName", ReleaseNameOperators, context => context.ReleaseName),
        CreateEnumerationField("Resolution", ResolutionOperators, context => context.Resolution),
        CreateTextField("PrimaryLanguage", LanguageOperators, context => context.PrimaryLanguage),
        new RuleFieldDescriptor(
            Name: "IsMultiLanguage",
            ValueKind: RuleFieldValueKind.Boolean,
            AllowedOperators: [RuleConditionOperator.Equals],
            Options: [],
            GetValue: context => context.IsMultiLanguage,
            ParseValue: value => bool.Parse(value)
        ),
        CreateEnumerationField("ContentType", EnumerationOperators, context => context.ContentType),
        CreateEnumerationField("Platform", EnumerationOperators, context => context.Platform),
        CreateEnumerationField("Source", EnumerationOperators, context => context.Source),
        CreateTextField("ReleaseGroupName", TextOperators, context => context.ReleaseGroupName),
        CreateTextField("ReleaseGroupToken", TextOperators, context => context.ReleaseGroupToken),
        CreateIntegerField("Year", context => context.Year),
        CreateIntegerField("Season", context => context.Season),
        CreateIntegerField("Episode", context => context.Episode) with
        {
            IsReleaseOnly = true,
        },
        CreateTextField("Genre", TextOperators, context => context.Genre),
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

    public static RuleFieldDescriptor Get(string name)
    {
        return FieldsByName[name];
    }

    private static RuleFieldDescriptor CreateTextField(
        string name,
        IReadOnlyList<RuleConditionOperator> allowedOperators,
        Func<ReleaseRoutingContext, string?> getValue
    )
    {
        return new RuleFieldDescriptor(
            Name: name,
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators: allowedOperators,
            Options: [],
            GetValue: getValue,
            ParseValue: value => value
        );
    }

    private static RuleFieldDescriptor CreateEnumerationField<TEnum>(
        string name,
        IReadOnlyList<RuleConditionOperator> allowedOperators,
        Func<ReleaseRoutingContext, TEnum> getValue
    )
        where TEnum : struct, Enum
    {
        return new RuleFieldDescriptor(
            Name: name,
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators: allowedOperators,
            Options: Enum.GetNames<TEnum>(),
            GetValue: context => getValue(context),
            ParseValue: value => Enum.Parse<TEnum>(value, ignoreCase: true)
        );
    }

    private static RuleFieldDescriptor CreateIntegerField(
        string name,
        Func<ReleaseRoutingContext, int?> getValue
    )
    {
        return new RuleFieldDescriptor(
            Name: name,
            ValueKind: RuleFieldValueKind.Integer,
            AllowedOperators: NumberOperators,
            Options: [],
            GetValue: context => getValue(context),
            ParseValue: value => int.Parse(value, CultureInfo.InvariantCulture)
        );
    }
}
