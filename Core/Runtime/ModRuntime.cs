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

    /// <summary>
    /// 按 manifest ID 查找游戏已识别的 MOD。
    /// </summary>
    /// <param name="modId">需要查找的 manifest ID。</param>
    /// <returns>匹配的 MOD；未找到时返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 本方法包含正在执行初始化器、尚未被游戏标记为 <see cref="ModLoadState.Loaded"/>
    /// 的 MOD。如果只希望查找已完成加载的 MOD，请使用 <see cref="FindLoadedMod"/>。
    /// </remarks>
    public static Mod? FindModById(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
        {
            return null;
        }

        return ModCompat.GetKnownMods().FirstOrDefault(mod =>
            string.Equals(
                ModCompat.GetManifestId(ModCompat.GetManifest(mod)),
                modId,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 在已完成加载的 MOD 中，按 manifest ID、PCK 名称、显示名称或程序集名称查找。
    /// </summary>
    /// <param name="modId">需要查找的 MOD 标识或名称。</param>
    /// <returns>匹配的已加载 MOD；未找到时返回 <see langword="null"/>。</returns>
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
