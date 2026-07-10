using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Modding;
using System.Collections;
using System.Reflection;

namespace JmcModLib.Compat;

/// <summary>
/// 封装不同 STS2 版本中的 MOD 列表、程序集与 manifest 成员差异。
/// </summary>
/// <remarks>
/// 已归档的游戏 DLL 中，0.99.1 至 0.107.1 的 <see cref="Mod"/> 使用单个
/// <c>assembly</c> 字段；0.108 将其改为 <c>assemblies</c> 列表，以支持一个 MOD
/// 关联多个托管程序集。0.99.1 的 MOD 列表与加载状态分别由
/// <c>AllMods</c>/<c>LoadedMods</c> 和 <c>wasLoaded</c> 表示；0.103 起改为
/// <c>Mods</c>/<c>GetLoadedMods()</c> 和 <c>state</c>。其他 PascalCase 候选名用于
/// 防御性兼容，不表示已确认它们存在于上述归档版本。
/// </remarks>
public static class ModCompat
{
    private static readonly Lazy<Func<IEnumerable<Mod>?>> KnownModsAccessor = new(CreateKnownModsAccessor);
    private static readonly Lazy<Func<IEnumerable<Mod>?>> LoadedModsAccessor = new(CreateLoadedModsAccessor);

    // MemberAccessor 本身已缓存“类型 + 成员名”的查找结果和生成后的访问器。
    // 这些 Lazy 字段只额外缓存“一组版本候选名最终命中哪个”，避免每次重走候选遍历。
    private static readonly Lazy<MemberAccessor?> ModStateAccessor =
        CreateInstanceAccessor<Mod>("state", "State");
    private static readonly Lazy<MemberAccessor?> ModWasLoadedAccessor =
        CreateInstanceAccessor<Mod>("wasLoaded", "WasLoaded");
    private static readonly Lazy<MemberAccessor?> ModAssembliesAccessor =
        CreateInstanceAccessor<Mod>("assemblies", "Assemblies");
    private static readonly Lazy<MemberAccessor?> ModAssemblyAccessor =
        CreateInstanceAccessor<Mod>("assembly", "Assembly");
    private static readonly Lazy<MemberAccessor?> ModManifestAccessor =
        CreateInstanceAccessor<Mod>("manifest", "Manifest");
    private static readonly Lazy<MemberAccessor?> ModPckNameAccessor =
        CreateInstanceAccessor<Mod>("pckName", "PckName");
    private static readonly Lazy<MemberAccessor?> ManifestIdAccessor =
        CreateInstanceAccessor<ModManifest>("id", "Id");
    private static readonly Lazy<MemberAccessor?> ManifestNameAccessor =
        CreateInstanceAccessor<ModManifest>("name", "Name");
    private static readonly Lazy<MemberAccessor?> ManifestVersionAccessor =
        CreateInstanceAccessor<ModManifest>("version", "Version");

    /// <summary>
    /// 获取当前游戏已识别的 MOD 快照。
    /// </summary>
    /// <returns>当前可从游戏 MOD 管理器读取的 MOD；无法识别当前版本入口时返回空集合。</returns>
    public static IReadOnlyList<Mod> GetKnownMods()
    {
        try
        {
            return KnownModsAccessor.Value() is { } mods ? [.. mods] : [];
        }
        catch (Exception ex)
        {
            ModLogger.Warn("读取游戏 MOD 列表失败。", ex);
            return [];
        }
    }

    /// <summary>
    /// 获取当前已经加载的 MOD 快照。
    /// </summary>
    /// <returns>
    /// 0.99.1 中位于 <c>LoadedMods</c> 的 MOD，或 0.103 起加载状态为
    /// <see cref="ModLoadState.Loaded"/> 的 MOD。
    /// </returns>
    public static IReadOnlyList<Mod> GetLoadedMods()
    {
        try
        {
            // 0.99.1 有独立的 LoadedMods 快照；0.103 起由 GetLoadedMods() 动态筛选。
            if (LoadedModsAccessor.Value() is { } mods)
            {
                return [.. mods];
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn("读取游戏已加载 MOD 列表失败，将改为逐项检查加载状态。", ex);
        }

        return [.. GetKnownMods().Where(IsLoaded)];
    }

    /// <summary>
    /// 判断一个游戏 MOD 当前是否已经完成加载。
    /// </summary>
    /// <param name="mod">目标游戏 MOD。</param>
    /// <returns>
    /// 0.99.1 的 <c>wasLoaded</c> 为真，或 0.103 起加载状态为
    /// <see cref="ModLoadState.Loaded"/> 时返回 <see langword="true"/>。
    /// </returns>
    public static bool IsLoaded(Mod? mod)
    {
        // 0.103–0.108：加载结果改为 ModLoadState state。
        if (TryGetInstanceValue(mod, ModStateAccessor, out ModLoadState state))
        {
            return state == ModLoadState.Loaded;
        }

        // 0.99.1：还没有 ModLoadState，仅用 bool wasLoaded 记录是否加载。
        return TryGetInstanceValue(mod, ModWasLoadedAccessor, out bool wasLoaded) && wasLoaded;
    }

    /// <summary>
    /// 获取一个游戏 MOD 关联的全部托管程序集。
    /// </summary>
    /// <param name="mod">目标游戏 MOD。</param>
    /// <returns>去重后的托管程序集快照；<paramref name="mod"/> 为空时返回空集合。</returns>
    public static IReadOnlyList<Assembly> GetAssemblies(Mod? mod)
    {
        if (mod == null)
        {
            return [];
        }

        var assemblies = new List<Assembly>();
        // 0.108：Mod.assemblies 是 List<Assembly>，并允许 MOD 关联后续注册的动态程序集。
        if (TryGetInstanceValue(mod, ModAssembliesAccessor, out List<Assembly>? multipleAssemblies))
        {
            AddAssemblies(assemblies, multipleAssemblies);
        }

        // 0.99.1–0.107.1：Mod.assembly 是可空的单个 Assembly。
        if (TryGetInstanceValue(mod, ModAssemblyAccessor, out Assembly? singleAssembly))
        {
            AddAssembly(assemblies, singleAssembly);
        }

        return assemblies;
    }

    /// <summary>
    /// 获取一个游戏 MOD 的首个托管程序集。
    /// </summary>
    /// <param name="mod">目标游戏 MOD。</param>
    /// <returns>首个托管程序集；无法读取时返回 <see langword="null"/>。</returns>
    public static Assembly? GetPrimaryAssembly(Mod? mod)
    {
        IReadOnlyList<Assembly> assemblies = GetAssemblies(mod);
        return assemblies.Count > 0 ? assemblies[0] : null;
    }

    /// <summary>
    /// 判断一个游戏 MOD 是否关联指定托管程序集。
    /// </summary>
    /// <param name="mod">目标游戏 MOD。</param>
    /// <param name="assembly">需要查找的程序集。</param>
    /// <returns>存在同一程序集实例时返回 <see langword="true"/>。</returns>
    public static bool ContainsAssembly(Mod? mod, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return GetAssemblies(mod).Any(candidate => ReferenceEquals(candidate, assembly));
    }

    /// <summary>
    /// 获取游戏 MOD 的 manifest。
    /// </summary>
    /// <param name="mod">目标游戏 MOD。</param>
    /// <returns>manifest；无法读取时返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 0.99.1–0.107.1 的 <c>ModManifest</c> 是 class，0.108 改为 record；
    /// <see cref="Mod"/> 持有它的 <c>manifest</c> 字段名在归档版本中没有变化。
    /// </remarks>
    public static ModManifest? GetManifest(Mod? mod)
    {
        return TryGetInstanceValue(mod, ModManifestAccessor, out ModManifest? manifest)
            ? manifest
            : null;
    }

    /// <summary>
    /// 获取游戏 MOD 的 PCK 名称。
    /// </summary>
    /// <param name="mod">目标游戏 MOD。</param>
    /// <returns>PCK 名称；无法读取时返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 归档的 0.99.1–0.108 <see cref="Mod"/> 中未发现 <c>pckName</c> 成员；
    /// 此候选名是为历史非归档构建和未来构建保留的防御性兼容。
    /// </remarks>
    public static string? GetPckName(Mod? mod)
    {
        return TryGetInstanceValue(mod, ModPckNameAccessor, out string? pckName)
            ? pckName
            : null;
    }

    /// <summary>
    /// 获取 manifest 的稳定 MOD 标识。
    /// </summary>
    /// <param name="manifest">目标 manifest。</param>
    /// <returns>MOD 标识；无法读取时返回 <see langword="null"/>。</returns>
    public static string? GetManifestId(ModManifest? manifest)
    {
        return TryGetInstanceValue(manifest, ManifestIdAccessor, out string? id)
            ? id
            : null;
    }

    /// <summary>
    /// 获取 manifest 的显示名称。
    /// </summary>
    /// <param name="manifest">目标 manifest。</param>
    /// <returns>显示名称；无法读取时返回 <see langword="null"/>。</returns>
    public static string? GetManifestName(ModManifest? manifest)
    {
        return TryGetInstanceValue(manifest, ManifestNameAccessor, out string? name)
            ? name
            : null;
    }

    /// <summary>
    /// 获取 manifest 的版本字符串。
    /// </summary>
    /// <param name="manifest">目标 manifest。</param>
    /// <returns>版本字符串；无法读取时返回 <see langword="null"/>。</returns>
    public static string? GetManifestVersion(ModManifest? manifest)
    {
        return TryGetInstanceValue(manifest, ManifestVersionAccessor, out string? version)
            ? version
            : null;
    }

    private static Func<IEnumerable<Mod>?> CreateKnownModsAccessor()
    {
        // 0.99.1：全部已识别 MOD 位于 AllMods，已加载子集位于 LoadedMods。
        // 0.103–0.108：删除上述两个属性，全部 MOD 统一由 Mods 公开。
        MemberAccessor? member = CompatMemberResolver.FindReadableMember(
            typeof(ModManager),
            isStatic: true,
            "Mods",
            "AllMods",
            "LoadedMods");
        if (member != null)
        {
            return CreateStaticModSequenceAccessor(member);
        }

        // 仅作最后回退：0.103–0.108 提供 GetLoadedMods()，但它不包含未加载的已识别 MOD。
        MethodAccessor? method = CompatMemberResolver.FindMethod(
            typeof(ModManager),
            isStatic: true,
            "GetLoadedMods",
            []);
        return method == null
            ? static () => null
            : () => method.InvokeStatic<IEnumerable<Mod>?>();
    }

    private static Func<IEnumerable<Mod>?> CreateLoadedModsAccessor()
    {
        // 0.99.1：直接读取初始化完成后保存的 LoadedMods 快照。
        MemberAccessor? member = CompatMemberResolver.FindReadableMember(
            typeof(ModManager),
            isStatic: true,
            "LoadedMods");
        if (member != null)
        {
            return CreateStaticModSequenceAccessor(member);
        }

        // 0.103–0.108：改为根据 Mod.state 返回已加载 MOD 的方法。
        MethodAccessor? method = CompatMemberResolver.FindMethod(
            typeof(ModManager),
            isStatic: true,
            "GetLoadedMods",
            []);
        return method == null
            ? static () => null
            : () => method.InvokeStatic<IEnumerable<Mod>?>();
    }

    private static Func<IEnumerable<Mod>?> CreateStaticModSequenceAccessor(MemberAccessor accessor)
    {
        // 0.99.1 的 AllMods/LoadedMods 以及 0.103–0.108 的 Mods 都声明为 IReadOnlyList<Mod>。
        // 类型完全匹配时可直接命中 MemberAccessor 的 Func<IReadOnlyList<Mod>> 强类型委托。
        if (accessor.ValueType == typeof(IReadOnlyList<Mod>))
        {
            return () => accessor.GetValue<IReadOnlyList<Mod>?>();
        }

        // 未来若改成其他 IEnumerable<Mod> 实现，仍保留 object getter 的兼容回退。
        return () => accessor.GetValue<object?>() as IEnumerable<Mod>;
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

    private static Lazy<MemberAccessor?> CreateInstanceAccessor<TTarget>(params string[] memberNames)
    {
        return new Lazy<MemberAccessor?>(() =>
            CompatMemberResolver.FindReadableMember(
                typeof(TTarget),
                isStatic: false,
                memberNames));
    }

    private static bool TryGetInstanceValue<TTarget, TValue>(
        TTarget? instance,
        Lazy<MemberAccessor?> lazyAccessor,
        out TValue value)
        where TTarget : class
    {
        if (instance == null || lazyAccessor.Value is not { } accessor)
        {
            value = default!;
            return false;
        }

        // 归档 0.99.1–0.108 中已确认的 Mod/ModManifest 实例成员均使用小写或 camelCase 字段；
        // PascalCase 名称是非归档构建和未来改为属性时的防御性回退。
        // 当 TTarget/TValue 与 DLL 中的声明类型完全一致时，MemberAccessor 会直接使用 Func<TTarget, TValue>。
        value = accessor.GetValue<TTarget, TValue>(instance);
        return true;
    }
}
