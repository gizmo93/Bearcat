using System.Collections;
using System.Globalization;
using System.Reflection;
using Bearcat.Domain.Shared.ForumPostRendering;
using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;

public static class ForumPostTemplateDataNodeBuilder
{
    public static IReadOnlyList<ForumPostTemplateDataNode> Build(ScriptObject globals)
    {
        return BuildDictionaryChildren(globals);
    }

    private static ForumPostTemplateDataNode BuildNode(string name, object? value)
    {
        return value switch
        {
            null => new ForumPostTemplateDataNode(name, null, []),
            string text => new ForumPostTemplateDataNode(name, text, []),
            bool flag => new ForumPostTemplateDataNode(name, flag ? "true" : "false", []),
            IDictionary<string, object?> dictionary => new ForumPostTemplateDataNode(
                Name: name,
                Value: null,
                Children: BuildDictionaryChildren(dictionary)
            ),
            IEnumerable items => new ForumPostTemplateDataNode(
                Name: name,
                Value: null,
                Children: BuildListChildren(items)
            ),
            _ when IsScalar(value.GetType()) => new ForumPostTemplateDataNode(
                Name: name,
                Value: FormatScalar(value),
                Children: []
            ),
            _ => new ForumPostTemplateDataNode(name, null, BuildObjectChildren(value)),
        };
    }

    private static List<ForumPostTemplateDataNode> BuildDictionaryChildren(
        IDictionary<string, object?> dictionary
    )
    {
        return dictionary.Select(pair => BuildNode(pair.Key, pair.Value)).ToList();
    }

    private static List<ForumPostTemplateDataNode> BuildListChildren(IEnumerable items)
    {
        return items
            .Cast<object?>()
            .Select((item, index) => BuildNode($"[{index}]", item))
            .ToList();
    }

    private static List<ForumPostTemplateDataNode> BuildObjectChildren(object value)
    {
        return value
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.GetIndexParameters().Length == 0
                && ForumPostTemplateVariableCatalog.ShouldExposeMember(property)
            )
            .Select(property =>
                BuildNode(StandardMemberRenamer.Rename(property), property.GetValue(value))
            )
            .ToList();
    }

    private static bool IsScalar(Type type)
    {
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid);
    }

    private static string FormatScalar(object value)
    {
        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString() ?? string.Empty;
    }
}
