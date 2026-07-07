using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace JmcModLib.Security.Backends;

internal sealed class SecretIdentifier
{
    public SecretIdentifier(Assembly assembly, string key, string? scope)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Assembly = assembly;
        ModId = ModRegistry.GetModId(assembly);
        Key = key.Trim();
        Scope = string.IsNullOrWhiteSpace(scope) ? string.Empty : scope.Trim();
        ServiceName = $"JmcModLib.{ModId}";
        AccountName = string.IsNullOrEmpty(Scope) ? Key : $"{Key}::{Scope}";
        StorageEntryKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{ServiceName}\n{AccountName}")));
    }

    public Assembly Assembly { get; }

    public string ModId { get; }

    public string Key { get; }

    public string Scope { get; }

    public string ServiceName { get; }

    public string AccountName { get; }

    public string StorageEntryKey { get; }

    public string ModSecretDirectory => Path.Combine(GetUserDataDirectory(), "mods", "secrets", SanitizePathSegment(ModId));

    public string SecureFilePath => Path.Combine(ModSecretDirectory, "secrets.v1.json");

    public string WeakFilePath => Path.Combine(ModSecretDirectory, "weak-secrets.v1.json");

    private static string GetUserDataDirectory()
    {
        try
        {
            string userDataDir = Godot.OS.GetUserDataDir();
            if (!string.IsNullOrWhiteSpace(userDataDir))
            {
                return userDataDir;
            }
        }
        catch
        {
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(appData)
            ? AppContext.BaseDirectory
            : Path.Combine(appData, "SlayTheSpire2");
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
