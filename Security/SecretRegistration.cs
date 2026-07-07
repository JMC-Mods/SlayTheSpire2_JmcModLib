using System.Reflection;

namespace JmcModLib.Security;

internal sealed class SecretRegistration
{
    public SecretRegistration(
        Assembly assembly,
        string key,
        string group,
        Func<string>? scopeProvider,
        bool allowWeakFileProtection)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        Assembly = assembly;
        Key = key.Trim();
        Group = group.Trim();
        ScopeProvider = scopeProvider;
        AllowWeakFileProtection = allowWeakFileProtection;
    }

    public Assembly Assembly { get; }

    public string Key { get; }

    public string Group { get; }

    public Func<string>? ScopeProvider { get; }

    public bool AllowWeakFileProtection { get; }

    public string ModId => ModRegistry.GetModId(Assembly);

    public string ResolveScope()
    {
        if (ScopeProvider == null)
        {
            return string.Empty;
        }

        try
        {
            return ScopeProvider.Invoke()?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"解析 Secret 范围失败：{Key}", ex, Assembly);
            return string.Empty;
        }
    }
}
