using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class RuleFieldCatalog
{
    public const string ReleaseName = "ReleaseName";

    public const string Resolution = "Resolution";

    public const string PrimaryLanguage = "PrimaryLanguage";

    public const string IsMultiLanguage = "IsMultiLanguage";

    public const string ContentType = "ContentType";

    public const string Platform = "Platform";

    public const string Source = "Source";

    public const string ReleaseGroupName = "ReleaseGroupName";

    public const string ReleaseGroupToken = "ReleaseGroupToken";

    public const string Year = "Year";

    public const string Season = "Season";

    public const string Episode = "Episode";

    private static readonly IReadOnlyList<RuleFieldDescriptor> AllFields =
    [
        new RuleFieldDescriptor(
            Name: ReleaseName,
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators:
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.Like,
                RuleConditionOperator.NotLike,
                RuleConditionOperator.Regex,
            ],
            Options: [],
            ValueAccessor: context => context.ReleaseName,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: Resolution,
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators:
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
                RuleConditionOperator.GreaterOrEqual,
                RuleConditionOperator.LessOrEqual,
            ],
            Options: Enum.GetNames<ReleaseResolution>(),
            ValueAccessor: context => context.Resolution,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: PrimaryLanguage,
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators:
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
                RuleConditionOperator.IsSet,
                RuleConditionOperator.IsNotSet,
            ],
            Options: [],
            ValueAccessor: context => context.PrimaryLanguage,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: IsMultiLanguage,
            ValueKind: RuleFieldValueKind.Boolean,
            AllowedOperators: [RuleConditionOperator.Equals],
            Options: [],
            ValueAccessor: context => context.IsMultiLanguage,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: ContentType,
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators:
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
            ],
            Options: Enum.GetNames<ReleaseContentType>(),
            ValueAccessor: context => context.ContentType,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: Platform,
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators:
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
            ],
            Options: Enum.GetNames<ReleasePlatform>(),
            ValueAccessor: context => context.Platform,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: Source,
            ValueKind: RuleFieldValueKind.Enumeration,
            AllowedOperators:
            [
                RuleConditionOperator.Equals,
                RuleConditionOperator.NotEquals,
                RuleConditionOperator.In,
                RuleConditionOperator.NotIn,
            ],
            Options: Enum.GetNames<ReleaseSource>(),
            ValueAccessor: context => context.Source,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: ReleaseGroupName,
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators: TextIdentityOperators,
            Options: [],
            ValueAccessor: context => context.ReleaseGroupName,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: ReleaseGroupToken,
            ValueKind: RuleFieldValueKind.Text,
            AllowedOperators: TextIdentityOperators,
            Options: [],
            ValueAccessor: context => context.ReleaseGroupToken,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: Year,
            ValueKind: RuleFieldValueKind.Integer,
            AllowedOperators: NumberOperators,
            Options: [],
            ValueAccessor: context => context.Year,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: Season,
            ValueKind: RuleFieldValueKind.Integer,
            AllowedOperators: NumberOperators,
            Options: [],
            ValueAccessor: context => context.Season,
            SupportedEntityTypes: AllEntityTypes
        ),
        new RuleFieldDescriptor(
            Name: Episode,
            ValueKind: RuleFieldValueKind.Integer,
            AllowedOperators: NumberOperators,
            Options: [],
            ValueAccessor: context => context.Episode,
            SupportedEntityTypes: ReleaseOnlyEntityTypes
        ),
    ];

    private static readonly Dictionary<string, RuleFieldDescriptor> FieldsByName =
        AllFields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<RuleFieldDescriptor> Fields => AllFields;

    public static IReadOnlyList<RuleFieldDescriptor> GetFields(ForumPostTemplateType entityType)
    {
        return AllFields.Where(field => field.SupportedEntityTypes.Contains(entityType)).ToList();
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

    private static IReadOnlyList<ForumPostTemplateType> AllEntityTypes =>
        [ForumPostTemplateType.Release, ForumPostTemplateType.ReleaseCollection];

    private static IReadOnlyList<ForumPostTemplateType> ReleaseOnlyEntityTypes =>
        [ForumPostTemplateType.Release];
}
