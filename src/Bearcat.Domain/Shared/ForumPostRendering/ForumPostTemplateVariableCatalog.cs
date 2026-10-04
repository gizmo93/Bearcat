using System.Collections;
using System.Reflection;
using System.Text;

namespace Bearcat.Domain.Shared.ForumPostRendering;

public static class ForumPostTemplateVariableCatalog
{
    public static IReadOnlyList<ForumPostTemplateVariableNode> GetVariables(Type rootType)
    {
        return GetVariables(rootType, null);
    }

    public static bool ShouldExposeMember(MemberInfo member)
    {
        return member.GetCustomAttribute<ForumPostTemplateVariableAttribute>() is not null;
    }

    private static List<ForumPostTemplateVariableNode> GetVariables(Type type, string? prefix)
    {
        var nodes = new List<ForumPostTemplateVariableNode>();

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = property.GetCustomAttribute<ForumPostTemplateVariableAttribute>();
            if (attribute is null)
                continue;

            var name = ToSnakeCase(property.Name);
            var path = CombinePath(prefix, name);

            nodes.Add(CreateNode(property, attribute, name, path));
        }

        return nodes;
    }

    private static ForumPostTemplateVariableNode CreateNode(
        PropertyInfo property,
        ForumPostTemplateVariableAttribute attribute,
        string name,
        string path
    )
    {
        if (!string.IsNullOrWhiteSpace(attribute.LoopVariable))
        {
            var elementType =
                attribute.ElementType ?? GetEnumerableElementType(property.PropertyType);
            var children =
                elementType is null || IsSimple(elementType)
                    ? []
                    : GetVariables(elementType, attribute.LoopVariable);

            return ForumPostTemplateVariableNode.CreateLoopNode(
                name,
                path,
                attribute.Description,
                attribute.LoopVariable,
                children
            );
        }

        if (attribute.IncludeChildren)
        {
            return ForumPostTemplateVariableNode.CreateObjectNode(
                name,
                path,
                attribute.Description,
                GetVariables(property.PropertyType, path)
            );
        }

        return ForumPostTemplateVariableNode.CreateValueNode(name, path, attribute.Description);
    }

    private static Type? GetEnumerableElementType(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return type.GetInterfaces()
            .Concat([type])
            .Where(candidate => candidate.IsGenericType)
            .FirstOrDefault(candidate =>
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            )
            ?.GetGenericArguments()[0];
    }

    private static bool IsSimple(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType.IsPrimitive
            || underlyingType.IsEnum
            || underlyingType == typeof(string)
            || underlyingType == typeof(decimal)
            || underlyingType == typeof(DateTime)
            || underlyingType == typeof(Guid);
    }

    private static string CombinePath(string? prefix, string name)
    {
        return string.IsNullOrWhiteSpace(prefix) ? name : $"{prefix}.{name}";
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (
                index > 0
                && char.IsUpper(current)
                && (
                    char.IsLower(value[index - 1])
                    || (index + 1 < value.Length && char.IsLower(value[index + 1]))
                )
            )
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
