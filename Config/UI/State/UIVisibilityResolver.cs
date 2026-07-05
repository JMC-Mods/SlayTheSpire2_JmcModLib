using JmcModLib.Config.Entry;

namespace JmcModLib.Config.UI;

internal static class UIVisibilityResolver
{
    public static bool IsVisible(ConfigEntry entry)
    {
        UIVisibleWhenAttribute? attribute = entry.VisibleWhenAttribute;
        if (attribute == null)
        {
            return true;
        }

        var context = new ConfigUiContext(entry.Assembly, entry.SourceDeclaringType);
        if (!context.TryResolveEntry(attribute.DependsOn, out ConfigEntry? dependency))
        {
            ModLogger.Warn($"动态可见性 {entry.Key} 声明的依赖项 {attribute.DependsOn} 不存在，已隐藏该配置项。", entry.Assembly);
            return false;
        }

        try
        {
            bool matched = Matches(dependency, attribute);
            return attribute.Invert ? !matched : matched;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"动态可见性 {entry.Key} 判断失败，已隐藏该配置项。", ex, entry.Assembly);
            return false;
        }
    }

    public static IReadOnlyList<ConfigEntry> ResolveDependencyEntries(ConfigEntry entry)
    {
        UIVisibleWhenAttribute? attribute = entry.VisibleWhenAttribute;
        if (attribute == null)
        {
            return [];
        }

        var context = new ConfigUiContext(entry.Assembly, entry.SourceDeclaringType);
        if (context.TryResolveEntry(attribute.DependsOn, out ConfigEntry? dependency) && dependency != null)
        {
            return [dependency];
        }

        ModLogger.Warn($"动态可见性 {entry.Key} 声明的依赖项 {attribute.DependsOn} 不存在，变更后可能不会自动刷新。", entry.Assembly);
        return [];
    }

    private static bool Matches(ConfigEntry dependency, UIVisibleWhenAttribute attribute)
    {
        object? actualValue = dependency.GetValue();
        object? expectedValue = attribute.ExpectedValue;

        if (actualValue is string actualText && expectedValue is string expectedText)
        {
            StringComparison comparison = attribute.IgnoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(actualText, expectedText, comparison);
        }

        object? convertedExpected = ConfigValueConverter.Convert(expectedValue, dependency.ValueType);
        if (convertedExpected is string convertedExpectedText && actualValue is string convertedActualText)
        {
            StringComparison comparison = attribute.IgnoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(convertedActualText, convertedExpectedText, comparison);
        }

        return Equals(actualValue, convertedExpected);
    }
}
