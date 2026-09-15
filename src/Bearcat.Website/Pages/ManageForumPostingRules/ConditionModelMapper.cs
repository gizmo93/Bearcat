using Bearcat.Domain.Shared.ForumPostingRules;

namespace Bearcat.Website.Pages.ManageForumPostingRules;

public static class ConditionModelMapper
{
    public static ConditionNodeModel CreateGroup(RuleConditionKind kind)
    {
        return new ConditionNodeModel { Kind = kind, Children = [CreateComparison()] };
    }

    public static ConditionNodeModel CreateNot()
    {
        return new ConditionNodeModel
        {
            Kind = RuleConditionKind.Not,
            Children = [CreateComparison()],
        };
    }

    public static ConditionNodeModel CreateComparison()
    {
        var descriptor = RuleFieldCatalog.Fields[0];

        return new ConditionNodeModel
        {
            Kind = RuleConditionKind.Comparison,
            Field = descriptor.Name,
            Operator = descriptor.AllowedOperators[0],
        };
    }

    public static ConditionNodeModel CreateDefaultRoot()
    {
        return CreateGroup(RuleConditionKind.All);
    }

    public static void ApplyField(ConditionNodeModel model, string? fieldName)
    {
        model.Field = fieldName ?? string.Empty;
        model.Value = string.Empty;
        model.Values = [];

        if (!RuleFieldCatalog.TryGet(model.Field, out var descriptor))
        {
            return;
        }

        if (!descriptor.AllowedOperators.Contains(model.Operator))
        {
            model.Operator = descriptor.AllowedOperators[0];
        }
    }

    public static void ApplyOperator(
        ConditionNodeModel model,
        RuleConditionOperator conditionOperator
    )
    {
        var wasMultiValue = IsMultiValue(model.Operator);
        model.Operator = conditionOperator;

        if (wasMultiValue == IsMultiValue(conditionOperator))
        {
            return;
        }

        model.Value = string.Empty;
        model.Values = [];
    }

    public static bool IsMultiValue(RuleConditionOperator conditionOperator)
    {
        return conditionOperator is RuleConditionOperator.In or RuleConditionOperator.NotIn;
    }

    public static bool NeedsValue(RuleConditionOperator conditionOperator)
    {
        return conditionOperator
            is not (RuleConditionOperator.IsSet or RuleConditionOperator.IsNotSet);
    }

    public static ConditionNodeModel ToModel(RuleCondition? condition)
    {
        if (condition is null)
        {
            return CreateDefaultRoot();
        }

        switch (condition.Kind)
        {
            case RuleConditionKind.All:
            case RuleConditionKind.Any:
                return new ConditionNodeModel
                {
                    Kind = condition.Kind,
                    Children = condition.Children is { Count: > 0 }
                        ? condition.Children.Select(ToModel).ToList()
                        : [CreateComparison()],
                };
            case RuleConditionKind.Not:
                return new ConditionNodeModel
                {
                    Kind = RuleConditionKind.Not,
                    Children = [ToModel(condition.Child)],
                };
            default:
                return ToComparisonModel(condition);
        }
    }

    public static RuleCondition ToCondition(ConditionNodeModel model)
    {
        switch (model.Kind)
        {
            case RuleConditionKind.All:
            case RuleConditionKind.Any:
                return new RuleCondition
                {
                    Kind = model.Kind,
                    Children = model.Children.Select(ToCondition).ToList(),
                };
            case RuleConditionKind.Not:
                return new RuleCondition
                {
                    Kind = RuleConditionKind.Not,
                    Child = model.Children.Count > 0 ? ToCondition(model.Children[0]) : null,
                };
            default:
                return ToComparisonCondition(model);
        }
    }

    public static ConditionNodeModel FromJson(string? json)
    {
        return RuleConditionSerializer.TryDeserialize(json, out var condition)
            ? ToModel(condition)
            : CreateDefaultRoot();
    }

    public static string ToJson(ConditionNodeModel model)
    {
        return RuleConditionSerializer.Serialize(ToCondition(model));
    }

    private static ConditionNodeModel ToComparisonModel(RuleCondition condition)
    {
        var model = new ConditionNodeModel
        {
            Kind = RuleConditionKind.Comparison,
            Field = condition.Field ?? string.Empty,
            Value = condition.Value ?? string.Empty,
            Values = condition.Values is null ? [] : [.. condition.Values],
        };

        if (condition.Operator is not null)
        {
            model.Operator = condition.Operator.Value;

            return model;
        }

        model.Operator = RuleFieldCatalog.TryGet(model.Field, out var descriptor)
            ? descriptor.AllowedOperators[0]
            : RuleConditionOperator.Equals;

        return model;
    }

    private static RuleCondition ToComparisonCondition(ConditionNodeModel model)
    {
        var condition = new RuleCondition
        {
            Kind = RuleConditionKind.Comparison,
            Field = model.Field,
            Operator = model.Operator,
        };

        if (!NeedsValue(model.Operator))
        {
            return condition;
        }

        if (IsMultiValue(model.Operator))
        {
            condition.Values = [.. model.Values];

            return condition;
        }

        condition.Value = model.Value;

        return condition;
    }
}
