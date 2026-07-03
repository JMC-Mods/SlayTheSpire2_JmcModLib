// 文件用途：兼容不同 STS2 版本中 Mod 与托管程序集之间的字段形态。
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Modding;
using System.Collections;
using System.Reflection;

namespace JmcModLib.Core;

internal static class ModAssemblyCompat
{
    public static IReadOnlyList<Assembly> GetAssemblies(Mod? mod)
    {
        if (mod == null)
        {
            return Array.Empty<Assembly>();
        }

        var assemblies = new List<Assembly>();
        AddAssemblies(assemblies, GetInstanceMemberValue(mod, "assemblies", "Assemblies"));
        AddAssembly(assemblies, GetInstanceMemberValue(mod, "assembly", "Assembly") as Assembly);
        return assemblies;
    }

    public static Assembly? GetPrimaryAssembly(Mod? mod)
    {
        return GetAssemblies(mod).FirstOrDefault();
    }

    public static bool ContainsAssembly(Mod? mod, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return GetAssemblies(mod).Any(candidate => ReferenceEquals(candidate, assembly));
    }

    private static void AddAssemblies(List<Assembly> assemblies, object? value)
    {
        if (value is not IEnumerable enumerable || value is string)
        {
            return;
        }

        foreach (object? item in enumerable)
        {
            AddAssembly(assemblies, item as Assembly);
        }
    }

    private static void AddAssembly(List<Assembly> assemblies, Assembly? assembly)
    {
        if (assembly != null && assemblies.All(candidate => !ReferenceEquals(candidate, assembly)))
        {
            assemblies.Add(assembly);
        }
    }

    private static object? GetInstanceMemberValue(object instance, params string[] memberNames)
    {
        Type type = instance.GetType();
        foreach (string memberName in memberNames)
        {
            MemberAccessor? accessor = TryGetMember(type, memberName);
            if (accessor is { IsStatic: false })
            {
                return accessor.GetValue(instance);
            }
        }

        return null;
    }

    private static MemberAccessor? TryGetMember(Type type, string memberName)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            try
            {
                return MemberAccessor.Get(current, memberName);
            }
            catch (MissingMemberException)
            {
            }
        }

        return null;
    }
}
