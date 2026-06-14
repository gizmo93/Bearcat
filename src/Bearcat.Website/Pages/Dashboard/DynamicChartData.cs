using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Bearcat.Website.Pages.Dashboard;

/// <summary>
/// Ugly workaround, as BlazorBlueprint Charts resolve series by checking the properties of an object using reflection.
/// This creates runtime objects for the rows that contain only the required hosters as CLR properties, so they can be resolved by
/// the BlazorBlueprint charts.
/// Hopefully this can go away as soon as BlazorBlueprint supports Dictionary series.
/// </summary>
internal sealed class DynamicChartData
{
    private static readonly ModuleBuilder Module = CreateModule();
    private static readonly ConcurrentDictionary<string, Type> RowTypeCache = new();

    private readonly Type rowType;
    private readonly IReadOnlyDictionary<string, PropertyInfo> properties;
    private readonly List<object> rows = [];

    public DynamicChartData(IReadOnlyDictionary<string, Type> columns)
    {
        rowType = GetOrCreateRowType(columns);
        properties = columns.Keys.ToDictionary(name => name, name => rowType.GetProperty(name)!);
    }

    public IReadOnlyList<object> Rows => rows;

    public Row AddRow()
    {
        var instance = Activator.CreateInstance(rowType)!;
        rows.Add(instance);

        return new Row(instance, properties);
    }

    public readonly struct Row(
        object instance,
        IReadOnlyDictionary<string, PropertyInfo> properties
    )
    {
        public void Set(string column, object value) =>
            properties[column].SetValue(instance, value);

        public void Add(string column, int value)
        {
            var property = properties[column];
            var current = (int)(property.GetValue(instance) ?? 0);
            property.SetValue(instance, current + value);
        }
    }

    private static Type GetOrCreateRowType(IReadOnlyDictionary<string, Type> columns)
    {
        var signature = string.Join(
            ";",
            columns
                .OrderBy(column => column.Key, StringComparer.Ordinal)
                .Select(column => $"{column.Key}:{column.Value.FullName}")
        );

        return RowTypeCache.GetOrAdd(signature, _ => EmitRowType(columns));
    }

    private static Type EmitRowType(IReadOnlyDictionary<string, Type> columns)
    {
        var typeBuilder = Module.DefineType(
            $"ChartRow_{Guid.NewGuid():N}",
            TypeAttributes.Public | TypeAttributes.Class
        );

        foreach (var (name, type) in columns)
        {
            DefineAutoProperty(typeBuilder, name, type);
        }

        return typeBuilder.CreateType();
    }

    private static void DefineAutoProperty(TypeBuilder typeBuilder, string name, Type type)
    {
        var field = typeBuilder.DefineField($"_{name}", type, FieldAttributes.Private);
        var property = typeBuilder.DefineProperty(name, PropertyAttributes.None, type, null);

        const MethodAttributes accessor =
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

        var getter = typeBuilder.DefineMethod($"get_{name}", accessor, type, Type.EmptyTypes);
        var getterIl = getter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, field);
        getterIl.Emit(OpCodes.Ret);

        var setter = typeBuilder.DefineMethod($"set_{name}", accessor, null, [type]);
        var setterIl = setter.GetILGenerator();
        setterIl.Emit(OpCodes.Ldarg_0);
        setterIl.Emit(OpCodes.Ldarg_1);
        setterIl.Emit(OpCodes.Stfld, field);
        setterIl.Emit(OpCodes.Ret);

        property.SetGetMethod(getter);
        property.SetSetMethod(setter);
    }

    private static ModuleBuilder CreateModule()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Bearcat.Website.DynamicChartData"),
            AssemblyBuilderAccess.Run
        );

        return assembly.DefineDynamicModule("Main");
    }
}
