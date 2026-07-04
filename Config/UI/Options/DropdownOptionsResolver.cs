using JmcModLib.Config.Entry;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace JmcModLib.Config.UI;

internal static class DropdownOptionsResolver
{
    private static readonly string[] ProviderNameFormats =
    [
        "{0}Options",
        "Get{0}Options",
        "Build{0}Options"
    ];

    public static IReadOnlyList<string> Resolve(
        ConfigEntry entry,
        UIDropdownAttribute? dropdownAttribute,
        Type valueType)
    {
        Type actualType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        IReadOnlyList<string> options = entry.DropdownOptionsProviderAttribute != null
            ? TryResolveExplicitProviderOptions(entry, entry.DropdownOptionsProviderAttribute)
            : [];

        if (options.Count == 0)
        {
            options = actualType.IsEnum
                ? [.. Enum.GetNames(actualType).Where(option => dropdownAttribute?.Exclude.Contains(option, StringComparer.OrdinalIgnoreCase) != true)]
                : dropdownAttribute?.Options.Count > 0
                    ? dropdownAttribute.Options
                    : TryResolveConventionOptions(entry);
        }

        if (options.Count == 0)
        {
            options = [entry.GetValue()?.ToString() ?? string.Empty];
        }

        string[] filteredOptions = [.. options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.Ordinal)];

        return filteredOptions.Length > 0
            ? filteredOptions
            : [entry.GetValue()?.ToString() ?? string.Empty];
    }

    public static IReadOnlyList<string> ResolveEffective(
        ConfigEntry entry,
        UIDropdownAttribute? dropdownAttribute,
        Type valueType,
        Action<ConfigEntry, object?> setValue)
    {
        IReadOnlyList<string> options = Resolve(entry, dropdownAttribute, valueType);
        UIDropdownInvalidValuePolicy policy =
            entry.DropdownOptionsProviderAttribute?.InvalidValuePolicy ?? UIDropdownInvalidValuePolicy.KeepCurrent;

        if (options.Count == 0)
        {
            return [entry.GetValue()?.ToString() ?? string.Empty];
        }

        string currentText = entry.GetValue()?.ToString() ?? string.Empty;
        if (ContainsOption(options, currentText))
        {
            return options;
        }

        return policy switch
        {
            UIDropdownInvalidValuePolicy.SelectFirstAvailable => SelectFirstAvailable(entry, options, setValue),
            UIDropdownInvalidValuePolicy.ResetToDefault => ResetToDefault(entry, options, setValue),
            _ => EnsureDisplayOption(options, currentText)
        };
    }

    public static IReadOnlyList<ConfigEntry> ResolveDependencyEntries(ConfigEntry entry)
    {
        UIDropdownOptionsProviderAttribute? attribute = entry.DropdownOptionsProviderAttribute;
        if (attribute == null || attribute.DependsOn.Length == 0)
        {
            return [];
        }

        var context = new ConfigUiContext(entry.Assembly, entry.SourceDeclaringType);
        var dependencies = new List<ConfigEntry>();
        foreach (string dependencyName in attribute.DependsOn.Where(static name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal))
        {
            if (context.TryResolveEntry(dependencyName, out ConfigEntry? dependency) && dependency != null)
            {
                dependencies.Add(dependency);
                continue;
            }

            ModLogger.Warn($"动态下拉 {entry.Key} 声明的依赖项 {dependencyName} 不存在，变更后可能不会自动刷新。", entry.Assembly);
        }

        return dependencies;
    }

    private static IReadOnlyList<string> TryResolveExplicitProviderOptions(
        ConfigEntry entry,
        UIDropdownOptionsProviderAttribute providerAttribute)
    {
        if (entry.SourceDeclaringType == null)
        {
            ModLogger.Warn($"动态下拉 {entry.Key} 缺少来源类型，无法解析 provider {providerAttribute.ProviderName}。", entry.Assembly);
            return [];
        }

        object? rawOptions = InvokeProvider(entry, providerAttribute);
        return NormalizeOptions(rawOptions);
    }

    private static IReadOnlyList<string> TryResolveConventionOptions(ConfigEntry entry)
    {
        if (!TryResolveDeclaringTypeAndMember(entry, out Type? declaringType, out string? memberName))
        {
            return [];
        }

        foreach (string nameFormat in ProviderNameFormats)
        {
            string providerName = string.Format(nameFormat, memberName);
            object? rawOptions = InvokeProvider(declaringType, providerName);
            IReadOnlyList<string> options = NormalizeOptions(rawOptions);
            if (options.Count > 0)
            {
                return options;
            }
        }

        return [];
    }

    private static bool TryResolveDeclaringTypeAndMember(
        ConfigEntry entry,
        [NotNullWhen(true)]
        out Type? declaringType,
        [NotNullWhen(true)]
        out string? memberName)
    {
        declaringType = entry.SourceDeclaringType;
        memberName = entry.SourceMemberName;

        if (declaringType != null && !string.IsNullOrWhiteSpace(memberName))
        {
            return true;
        }

        declaringType = null;
        memberName = null;

        foreach (Type type in entry.Assembly.GetTypes().OrderByDescending(type => type.FullName?.Length ?? 0))
        {
            string? fullName = type.FullName;
            if (string.IsNullOrWhiteSpace(fullName)
                || !entry.StorageKey.StartsWith(fullName + ".", StringComparison.Ordinal))
            {
                continue;
            }

            string candidateMemberName = entry.StorageKey[(fullName.Length + 1)..];
            if (string.IsNullOrWhiteSpace(candidateMemberName) || candidateMemberName.Contains('.'))
            {
                continue;
            }

            declaringType = type;
            memberName = candidateMemberName;
            return true;
        }

        return false;
    }

    private static object? InvokeProvider(ConfigEntry entry, UIDropdownOptionsProviderAttribute providerAttribute)
    {
        Type declaringType = entry.SourceDeclaringType!;
        string providerName = providerAttribute.ProviderName;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        try
        {
            MethodInfo? method = ResolveProviderMethod(declaringType, providerName);
            if (method != null)
            {
                ParameterInfo[] parameters = method.GetParameters();
                object?[]? args = parameters.Length == 0
                    ? null
                    : [new ConfigUiContext(entry.Assembly, declaringType)];
                return method.Invoke(null, args);
            }

            PropertyInfo? property = declaringType.GetProperty(providerName, flags);
            if (property != null)
            {
                return property.GetValue(null);
            }

            ModLogger.Warn($"找不到动态下拉选项 provider {declaringType.FullName}.{providerName}。", entry.Assembly);
            return null;
        }
        catch (TargetInvocationException ex)
        {
            Exception inner = ex.InnerException ?? ex;
            ModLogger.Warn($"动态下拉选项 provider {declaringType.FullName}.{providerName} 执行失败：{inner.Message}", inner, entry.Assembly);
            return null;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"动态下拉选项 provider {declaringType.FullName}.{providerName} 执行失败：{ex.Message}", ex, entry.Assembly);
            return null;
        }
    }

    private static MethodInfo? ResolveProviderMethod(Type declaringType, string providerName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        foreach (MethodInfo method in declaringType.GetMethods(flags).Where(method => method.Name == providerName))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                return method;
            }

            if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(ConfigUiContext)))
            {
                return method;
            }
        }

        return null;
    }

    private static object? InvokeProvider(Type declaringType, string providerName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        try
        {
            MethodInfo? method = declaringType.GetMethod(providerName, flags, Type.EmptyTypes);
            if (method != null)
            {
                return method.Invoke(null, null);
            }

            PropertyInfo? property = declaringType.GetProperty(providerName, flags);
            return property?.GetValue(null);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"动态下拉选项 provider {declaringType.FullName}.{providerName} 执行失败：{ex.Message}");
            return null;
        }
    }

    private static IReadOnlyList<string> NormalizeOptions(object? rawOptions)
    {
        if (rawOptions == null)
        {
            return [];
        }

        if (rawOptions is string singleOption)
        {
            return [singleOption];
        }

        if (rawOptions is IEnumerable<string> stringOptions)
        {
            return [.. stringOptions];
        }

        if (rawOptions is IEnumerable options)
        {
            return [.. options.Cast<object?>().Select(option => option?.ToString() ?? string.Empty)];
        }

        return [rawOptions.ToString() ?? string.Empty];
    }

    private static IReadOnlyList<string> SelectFirstAvailable(
        ConfigEntry entry,
        IReadOnlyList<string> options,
        Action<ConfigEntry, object?> setValue)
    {
        setValue(entry, options[0]);
        return options;
    }

    private static IReadOnlyList<string> ResetToDefault(
        ConfigEntry entry,
        IReadOnlyList<string> options,
        Action<ConfigEntry, object?> setValue)
    {
        setValue(entry, entry.DefaultValue);
        string defaultText = entry.DefaultValue?.ToString() ?? string.Empty;
        return ContainsOption(options, defaultText) ? options : EnsureDisplayOption(options, defaultText);
    }

    private static IReadOnlyList<string> EnsureDisplayOption(IReadOnlyList<string> options, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || ContainsOption(options, value))
        {
            return options;
        }

        return [.. options, value];
    }

    private static bool ContainsOption(IReadOnlyList<string> options, string value)
    {
        return options.Any(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));
    }
}
