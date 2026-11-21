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
        CreateTextField(
            "ReleaseName",
            ReleaseNameOperators,
            fieldValues => fieldValues.ReleaseName
        ),
        CreateEnumerationField(
            "Resolution",
            ResolutionOperators,
            fieldValues => fieldValues.Resolution
        ),
        CreateTextField(
            "PrimaryLanguage",
            LanguageOperators,
            fieldValues => fieldValues.PrimaryLanguage
        ),
        new RuleFieldDescriptor(
            Name: "IsMultiLanguage",
            ValueKind: RuleFieldValueKind.Boolean,
            AllowedOperators: [RuleConditionOperator.Equals],
            Options: [],
            GetValue: fieldValues => fieldValues.IsMultiLanguage,
            ParseValue: value => bool.Parse(value)
        ),
        CreateEnumerationField(
            "ContentType",
            EnumerationOperators,
            fieldValues => fieldValues.ContentType
        ),
        CreateEnumerationField(
            "Platform",
            EnumerationOperators,
            fieldValues => fieldValues.Platform
        ),
        CreateEnumerationField("Source", EnumerationOperators, fieldValues => fieldValues.Source),
        CreateTextField(
            "ReleaseGroupName",
            TextOperators,
            fieldValues => fieldValues.ReleaseGroupName
        ),
        CreateTextField(
            "ReleaseGroupToken",
            TextOperators,
            fieldValues => fieldValues.ReleaseGroupToken
        ),
        CreateIntegerField("Year", fieldValues => fieldValues.Year),
        CreateIntegerField("Season", fieldValues => fieldValues.Season),
        CreateIntegerField("Episode", fieldValues => fieldValues.Episode) with
        {
            IsReleaseOnly = true,
        },
        CreateTextField("Genre", TextOperators, fieldValues => fieldValues.Genre),
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

    public static RuleFieldDescriptor Get(string name)
    {
        return FieldsByName[name];
    }

    private static RuleFieldDescriptor CreateTextField(
        string name,
        IReadOnlyList<RuleConditionOperator> allowedOperators,
        Func<RuleFieldValues, string?> getValue
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
        Func<RuleFieldValues, TEnum> getValue
    )
        where TEnum : struct, Enum
    {
        return new RuleFieldDescriptor(
            Name: name,
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators: allowedOperators,
            Options: Enum.GetNames<TEnum>(),
            GetValue: fieldValues => getValue(fieldValues),
            ParseValue: value => Enum.Parse<TEnum>(value, ignoreCase: true)
        );
    }

    private static RuleFieldDescriptor CreateIntegerField(
        string name,
        Func<RuleFieldValues, int?> getValue
    )
    {
        return new RuleFieldDescriptor(
            Name: name,
            ValueKind: RuleFieldValueKind.Integer,
            AllowedOperators: NumberOperators,
            Options: [],
            GetValue: fieldValues => getValue(fieldValues),
            ParseValue: value => int.Parse(value, CultureInfo.InvariantCulture)
        );
    }
}
