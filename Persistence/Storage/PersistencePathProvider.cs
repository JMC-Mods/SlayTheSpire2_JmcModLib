using Godot;
using JmcModLib.Persistence.Run;
using MegaCrit.Sts2.Core.Saves;
using System.Reflection;

namespace JmcModLib.Persistence.Storage;

internal static class PersistencePathProvider
{
    public static bool TryGetFilePath(PersistenceScope scope, Assembly assembly, out string filePath)
    {
        filePath = string.Empty;

        try
        {
            string modId = PersistenceIdentifier.SanitizePathSegment(ModRegistry.GetModId(assembly));
            switch (scope)
            {
                case PersistenceScope.LocalPreference:
                    filePath = Path.Combine(
                        GetLocalUserDataDir(),
                        "mods",
                        "persistence",
                        modId,
                        "local-preferences.v1.json");
                    return true;

                case PersistenceScope.Global:
                    string globalBase = UserDataPathProvider.GetAccountScopedBasePath($"mods/persistence/{modId}");
                    filePath = Path.Combine(Globalize(globalBase), "global.v1.json");
                    return true;

                case PersistenceScope.Profile:
                    SaveManager saveManager = SaveManager.Instance;
                    if (!saveManager.IsProfileInitialized)
                    {
                        return false;
                    }

                    string profilePath = saveManager.GetProfileScopedPath($"mods/persistence/{modId}/profile.v1.json");
                    filePath = Globalize(profilePath);
                    return true;

                case PersistenceScope.ClientRun:
                    if (!RunPersistenceManager.TryGetCurrentClientRunIdentity(out RunIdentity identity))
                    {
                        return false;
                    }

                    return TryGetClientRunFilePath(assembly, identity, out filePath);

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"解析 Persistence 存储路径失败：{scope}", ex, assembly);
            return false;
        }
    }

    public static bool TryGetClientRunFilePath(Assembly assembly, RunIdentity identity, out string filePath)
    {
        filePath = string.Empty;

        try
        {
            string modId = PersistenceIdentifier.SanitizePathSegment(ModRegistry.GetModId(assembly));
            string profileId = PersistenceIdentifier.SanitizePathSegment(identity.ProfileId.ToString());
            string runIdentity = PersistenceIdentifier.SanitizePathSegment(identity.FileStem);
            filePath = Path.Combine(
                GetLocalUserDataDir(),
                "mods",
                "persistence",
                modId,
                "client-runs",
                profileId,
                $"{runIdentity}.v1.json");
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn("解析客户端本局 Persistence 存储路径失败。", ex, assembly);
            return false;
        }
    }

    private static string GetLocalUserDataDir()
    {
        try
        {
            string userData = OS.GetUserDataDir();
            if (!string.IsNullOrWhiteSpace(userData))
            {
                return userData;
            }
        }
        catch
        {
        }

        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "SlayTheSpire2");
    }

    private static string Globalize(string path)
    {
        try
        {
            string normalized = path.Replace('\\', '/');
            string globalized = ProjectSettings.GlobalizePath(normalized);
            if (!string.IsNullOrWhiteSpace(globalized))
            {
                return globalized;
            }
        }
        catch
        {
            // Godot 尚未完全可用时走后备路径。
        }

        string userData = GetLocalUserDataDir();

        if (path.StartsWith("user://", StringComparison.Ordinal))
        {
            return Path.Combine(userData, path["user://".Length..].Replace('/', Path.DirectorySeparatorChar));
        }

        return path;
    }
}
