using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class RuleFieldCatalog
{
    public const string ReleaseName = "ReleaseName";

    public const string Resolution = "Resolution";

    public const string PrimaryLanguage = "PrimaryLanguage";

    public const string IsMultiLanguage = "IsMultiLanguage";

    public const string ContentType = "ContentType";

    public const string Source = "Source";

    public const string ReleaseGroupName = "ReleaseGroupName";

    public const string ReleaseGroupToken = "ReleaseGroupToken";

    public const string Year = "Year";

    public const string Season = "Season";

    public const string Episode = "Episode";

    private static readonly IReadOnlyList<RuleFieldDescriptor> AllFields =
    [
        new RuleFieldDescriptor(
            ReleaseName,
            RuleFieldValueKind.Text,
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.Like,
                RuleConditionOperator.NotLike,
                RuleConditionOperator.Regex,
            ],
            [],
            context => context.ReleaseName
        ),
        new RuleFieldDescriptor(
            Resolution,
            RuleFieldValueKind.Enumeration,
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
                RuleConditionOperator.GreaterOrEqual,
                RuleConditionOperator.LessOrEqual,
            ],
            Enum.GetNames<ReleaseResolution>(),
            context => context.Resolution
        ),
        new RuleFieldDescriptor(
            PrimaryLanguage,
            RuleFieldValueKind.Text,
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
                RuleConditionOperator.IsSet,
                RuleConditionOperator.IsNotSet,
            ],
            [],
            context => context.PrimaryLanguage
        ),
        new RuleFieldDescriptor(
            IsMultiLanguage,
            RuleFieldValueKind.Boolean,
            [RuleConditionOperator.Equals],
            [],
            context => context.IsMultiLanguage
        ),
        new RuleFieldDescriptor(
            ContentType,
            RuleFieldValueKind.Enumeration,
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
            ],
            Enum.GetNames<ReleaseContentType>(),
            context => context.ContentType
        ),
        new RuleFieldDescriptor(
            Source,
            RuleFieldValueKind.Enumeration,
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
            ],
            Enum.GetNames<ReleaseSource>(),
            context => context.Source
        ),
        new RuleFieldDescriptor(
            ReleaseGroupName,
            RuleFieldValueKind.Text,
            TextIdentityOperators,
            [],
            context => context.ReleaseGroupName
        ),
        new RuleFieldDescriptor(
            ReleaseGroupToken,
            RuleFieldValueKind.Text,
            TextIdentityOperators,
            [],
            context => context.ReleaseGroupToken
        ),
        new RuleFieldDescriptor(
            Year,
            RuleFieldValueKind.Integer,
            NumberOperators,
            [],
            context => context.Year
        ),
        new RuleFieldDescriptor(
            Season,
            RuleFieldValueKind.Integer,
            NumberOperators,
            [],
            context => context.Season
        ),
        new RuleFieldDescriptor(
            Episode,
            RuleFieldValueKind.Integer,
            NumberOperators,
            [],
            context => context.Episode
        ),
    ];

    private static readonly Dictionary<string, RuleFieldDescriptor> FieldsByName =
        AllFields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<RuleFieldDescriptor> Fields => AllFields;

    public static bool TryGet(string? name, out RuleFieldDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            descriptor = null!;

            return false;
        }

        return FieldsByName.TryGetValue(name, out descriptor!);
    }

    private static IReadOnlyList<RuleConditionOperator> TextIdentityOperators =>
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

    private static IReadOnlyList<RuleConditionOperator> NumberOperators =>
        [
            RuleConditionOperator.Equals,
            RuleConditionOperator.NotEquals,
            RuleConditionOperator.GreaterOrEqual,
            RuleConditionOperator.LessOrEqual,
            RuleConditionOperator.IsSet,
            RuleConditionOperator.IsNotSet,
        ];
}
