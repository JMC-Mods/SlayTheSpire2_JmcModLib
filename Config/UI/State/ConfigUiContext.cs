using JmcModLib.Config.Entry;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace JmcModLib.Config.UI;

internal sealed class ConfigUiContext(Assembly assembly, Type? preferredDeclaringType = null) : IConfigUiContext
{
    public T Get<T>(string key)
    {
        if (TryGet(key, out object? rawValue))
        {
            return ConfigValueConverter.Convert<T>(rawValue);
        }

        throw new KeyNotFoundException($"找不到配置项：{key}");
    }

    public bool TryGet<T>(string key, out T value)
    {
        if (TryGet(key, out object? rawValue))
        {
            try
            {
                value = ConfigValueConverter.Convert<T>(rawValue);
                return true;
            }
            catch
            {
                value = default!;
                return false;
            }
        }

        value = default!;
        return false;
    }

    public object? Get(string key)
    {
        if (TryGet(key, out object? value))
        {
            return value;
        }

        throw new KeyNotFoundException($"找不到配置项：{key}");
    }

    public bool TryGet(string key, out object? value)
    {
        if (TryResolveEntry(key, out ConfigEntry? entry))
        {
            value = entry.GetValue();
            return true;
        }

        value = null;
        return false;
    }

    internal bool TryResolveEntry(string key, [NotNullWhen(true)] out ConfigEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        string normalizedKey = key.Trim();
        ConfigEntry[] entries = [.. ConfigManager.GetEntries(assembly)];

        entry = entries.FirstOrDefault(candidate =>
            candidate.SourceDeclaringType == preferredDeclaringType
            && string.Equals(candidate.SourceMemberName, normalizedKey, StringComparison.Ordinal));
        if (entry != null)
        {
            return true;
        }

        entry = entries.FirstOrDefault(candidate =>
            string.Equals(candidate.SourceMemberName, normalizedKey, StringComparison.Ordinal));
        if (entry != null)
        {
            return true;
        }

        entry = entries.FirstOrDefault(candidate =>
            string.Equals(candidate.StorageKey, normalizedKey, StringComparison.Ordinal)
            || string.Equals(candidate.Key, normalizedKey, StringComparison.Ordinal));
        return entry != null;
    }
}
