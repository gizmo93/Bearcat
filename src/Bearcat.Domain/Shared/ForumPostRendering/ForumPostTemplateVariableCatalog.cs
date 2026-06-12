using System.Collections;
using System.Reflection;
using System.Text;

namespace Bearcat.Domain.Shared.ForumPostRendering;

public static class ForumPostTemplateVariableCatalog
{
    public static IReadOnlyList<ForumPostTemplateVariableReadModel> GetVariables(Type rootType)
    {
        return GetVariables(rootType, null).ToList();
    }

    public static bool ShouldExposeMember(MemberInfo member)
    {
        return member.GetCustomAttribute<ForumPostTemplateVariableAttribute>() is not null;
    }

    private static IEnumerable<ForumPostTemplateVariableReadModel> GetVariables(
        Type type,
        string? prefix
    )
    {
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = property.GetCustomAttribute<ForumPostTemplateVariableAttribute>();
            if (attribute is null)
                continue;

            var path = CombinePath(prefix, ToSnakeCase(property.Name));

            foreach (var variable in GetVariablesForProperty(property, attribute, path))
                yield return variable;
        }
    }

    private static IEnumerable<ForumPostTemplateVariableReadModel> GetVariablesForProperty(
        PropertyInfo property,
        ForumPostTemplateVariableAttribute attribute,
        string path
    )
    {
        if (!string.IsNullOrWhiteSpace(attribute.LoopVariable))
        {
            yield return new ForumPostTemplateVariableReadModel(
                $"{{{{ for {attribute.LoopVariable} in {path} }}}}",
                attribute.Description
            );

            var elementType =
                attribute.ElementType ?? GetEnumerableElementType(property.PropertyType);
            if (elementType is not null && !IsSimple(elementType))
            {
                foreach (var child in GetVariables(elementType, attribute.LoopVariable))
                    yield return child;
            }

            yield break;
        }

        if (attribute.IncludeChildren)
        {
            foreach (var child in GetVariables(property.PropertyType, path))
                yield return child;

            yield break;
        }

        yield return new ForumPostTemplateVariableReadModel(
            $"{{{{ {path} }}}}",
            attribute.Description
        );
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
