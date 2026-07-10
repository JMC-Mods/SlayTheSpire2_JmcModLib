// 文件用途：封装 STS2 运行时 MOD/manifest 查询，帮助 JML 从游戏加载状态推断 MOD 信息。
using JmcModLib.Compat;
using MegaCrit.Sts2.Core.Modding;
using System.Reflection;

namespace JmcModLib.Core;

public static class ModRuntime
{
    public static Mod? TryGetLoadedMod(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        return ModCompat.GetLoadedMods().FirstOrDefault(mod => ModCompat.ContainsAssembly(mod, assembly));
    }

    public static ModManifest? TryGetManifest(Assembly? assembly = null)
    {
        return ModCompat.GetManifest(TryGetLoadedMod(assembly));
    }

    public static string? GetManifestId(Assembly? assembly = null)
    {
        return ModCompat.GetManifestId(TryGetManifest(assembly));
    }

    public static string GetPckName(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        return ModCompat.GetPckName(TryGetLoadedMod(assembly))
            ?? assembly.GetName().Name
            ?? VersionInfo.Name;
    }

    public static string GetDisplayName(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        return ModCompat.GetManifestName(TryGetManifest(assembly))
            ?? assembly.GetName().Name
            ?? VersionInfo.Name;
    }

    public static Version? GetLoadedVersion(Assembly? assembly = null)
    {
        assembly = ResolveAssembly(assembly);
        string? rawVersion = ModCompat.GetManifestVersion(TryGetManifest(assembly));
        if (Version.TryParse(rawVersion, out Version? parsed))
        {
            return parsed;
        }

        return assembly.GetName().Version;
    }

    public static Mod? FindModById(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        return ModCompat.GetLoadedMods().FirstOrDefault(mod =>
            string.Equals(
                ModCompat.GetManifestId(ModCompat.GetManifest(mod)),
                modId,
                StringComparison.OrdinalIgnoreCase));
    }

    public static Mod? FindLoadedMod(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        return ModCompat.GetLoadedMods().FirstOrDefault(mod =>
        {
            Assembly? modAssembly = ModCompat.GetPrimaryAssembly(mod);
            ModManifest? manifest = ModCompat.GetManifest(mod);
            return string.Equals(ModCompat.GetManifestId(manifest), modId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ModCompat.GetPckName(mod), modId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ModCompat.GetManifestName(manifest), modId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(modAssembly?.GetName().Name, modId, StringComparison.OrdinalIgnoreCase)
                || ModCompat.GetAssemblies(mod).Any(candidate =>
                    string.Equals(candidate.GetName().Name, modId, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static Assembly ResolveAssembly(Assembly? assembly)
    {
        return AssemblyResolver.Resolve(assembly, typeof(ModRuntime));
    }
}
