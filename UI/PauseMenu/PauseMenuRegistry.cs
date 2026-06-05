using JmcModLib.Core.AttributeRouter;
using System.Collections.Concurrent;
using System.Reflection;

namespace JmcModLib.UI.PauseMenu;

/// <summary>
/// 管理子 MOD 注册到运行中暂停菜单的按钮条目。
/// </summary>
/// <remarks>
/// <para>
/// 注册身份为“所属程序集 + 按钮键”。同一程序集重复注册相同键时，新条目会替换旧条目；
/// 不同程序集可以使用相同键并稳定共存。
/// </para>
/// </remarks>
public static class PauseMenuRegistry
{
    private static readonly ConcurrentDictionary<Assembly, ConcurrentDictionary<string, PauseMenuButtonEntry>> Entries = new();
    private static readonly ConcurrentDictionary<string, byte> WarnedDuplicateModIds = new(StringComparer.Ordinal);
    private static int initialized;

    internal static bool IsInitialized => Volatile.Read(ref initialized) == 1;

    internal static void Init()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return;
        }

        AttributeRouter.Init();
        AttributeRouter.RegisterHandler<PauseMenuButtonAttribute>(new PauseMenuButtonAttributeHandler());
        ModRegistry.OnUnregistered += OnModUnregistered;
        ModLogger.Debug("PauseMenuRegistry initialized.");
    }

    /// <summary>
    /// 注册一个无上下文参数的同步暂停菜单按钮。
    /// </summary>
    /// <param name="options">按钮选项，必须包含 <see cref="PauseMenuButtonOptions.Key"/> 和 <see cref="PauseMenuButtonOptions.Text"/>。</param>
    /// <param name="action">点击按钮时执行的动作。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    public static void RegisterButton(PauseMenuButtonOptions options, Action action, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        RegisterButton(options, _ =>
        {
            action();
            return Task.CompletedTask;
        }, assembly);
    }

    /// <summary>
    /// 注册一个带上下文参数的同步暂停菜单按钮。
    /// </summary>
    /// <param name="options">按钮选项，必须包含 <see cref="PauseMenuButtonOptions.Key"/> 和 <see cref="PauseMenuButtonOptions.Text"/>。</param>
    /// <param name="action">点击按钮时执行的动作。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    public static void RegisterButton(PauseMenuButtonOptions options, Action<PauseMenuButtonContext> action, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        RegisterButton(options, context =>
        {
            action(context);
            return Task.CompletedTask;
        }, assembly);
    }

    /// <summary>
    /// 注册一个无上下文参数的异步暂停菜单按钮。
    /// </summary>
    /// <param name="options">按钮选项，必须包含 <see cref="PauseMenuButtonOptions.Key"/> 和 <see cref="PauseMenuButtonOptions.Text"/>。</param>
    /// <param name="action">点击按钮时执行的异步动作。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    public static void RegisterButton(PauseMenuButtonOptions options, Func<Task> action, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        RegisterButton(options, _ => action(), assembly);
    }

    /// <summary>
    /// 注册一个带上下文参数的异步暂停菜单按钮。
    /// </summary>
    /// <param name="options">按钮选项，必须包含 <see cref="PauseMenuButtonOptions.Key"/> 和 <see cref="PauseMenuButtonOptions.Text"/>。</param>
    /// <param name="action">点击按钮时执行的异步动作。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    public static void RegisterButton(
        PauseMenuButtonOptions options,
        Func<PauseMenuButtonContext, Task> action,
        Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        RegisterInternal(AssemblyResolver.Resolve(assembly, typeof(PauseMenuRegistry)), options, action);
    }

    /// <summary>
    /// 注销当前 MOD 中指定键的暂停菜单按钮。
    /// </summary>
    /// <param name="key">按钮注册键。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    /// <returns>成功移除按钮时返回 <see langword="true"/>。</returns>
    public static bool UnregisterButton(string key, Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(PauseMenuRegistry));
        return Entries.TryGetValue(resolvedAssembly, out ConcurrentDictionary<string, PauseMenuButtonEntry>? lookup)
            && lookup.TryRemove(key.Trim(), out _);
    }

    /// <summary>
    /// 注销指定程序集下的所有暂停菜单按钮。
    /// </summary>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    public static void UnregisterAssembly(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(PauseMenuRegistry));
        _ = Entries.TryRemove(resolvedAssembly, out _);
    }

    /// <summary>
    /// 获取指定程序集当前注册的暂停菜单按钮选项快照。
    /// </summary>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    /// <returns>按钮选项快照集合。</returns>
    public static IReadOnlyCollection<PauseMenuButtonOptions> GetEntries(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(PauseMenuRegistry));
        return Entries.TryGetValue(resolvedAssembly, out ConcurrentDictionary<string, PauseMenuButtonEntry>? lookup)
            ? lookup.Values.Select(static entry => entry.ToOptions()).ToArray()
            : [];
    }

    internal static IReadOnlyList<PauseMenuButtonEntry> GetAllEntriesSorted()
    {
        return Entries.Values
            .SelectMany(static lookup => lookup.Values)
            .OrderBy(static entry => entry.Anchor)
            .ThenBy(static entry => entry.Order)
            .ThenBy(static entry => entry.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.AssemblyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool TryGetEntry(string assemblyFullName, string key, out PauseMenuButtonEntry? entry)
    {
        entry = null;
        foreach ((Assembly assembly, ConcurrentDictionary<string, PauseMenuButtonEntry> lookup) in Entries)
        {
            if (string.Equals(assembly.FullName, assemblyFullName, StringComparison.Ordinal)
                && lookup.TryGetValue(key, out PauseMenuButtonEntry? match))
            {
                entry = match;
                return true;
            }
        }

        return false;
    }

    internal static void RegisterInternal(
        Assembly assembly,
        PauseMenuButtonOptions options,
        Func<PauseMenuButtonContext, Task> action)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(action);
        Init();

        string key = NormalizeRequired(options.Key, nameof(options.Key));
        string text = NormalizeRequired(options.Text, nameof(options.Text));
        var entry = new PauseMenuButtonEntry(
            assembly,
            key,
            text,
            options.Anchor,
            options.Order,
            options.LocTable,
            options.TextKey,
            options.VisibleWhen,
            options.EnabledWhen,
            options.CloseMenuOnClick,
            options.Color,
            action);

        ConcurrentDictionary<string, PauseMenuButtonEntry> lookup = Entries.GetOrAdd(
            assembly,
            static _ => new ConcurrentDictionary<string, PauseMenuButtonEntry>(StringComparer.Ordinal));

        if (lookup.ContainsKey(key))
        {
            ModLogger.Warn($"暂停菜单按钮 {key} 已经注册，将使用新的注册替换旧条目。", assembly);
        }

        lookup[key] = entry;
        WarnIfDuplicateModId(entry);
    }

    private static string NormalizeRequired(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", name);
        }

        return value.Trim();
    }

    private static void WarnIfDuplicateModId(PauseMenuButtonEntry entry)
    {
        string modId = entry.ModId;
        Assembly? otherAssembly = Entries
            .Select(static pair => pair.Key)
            .FirstOrDefault(assembly => assembly != entry.Assembly
                && string.Equals(ModRegistry.GetModId(assembly), modId, StringComparison.Ordinal));

        if (otherAssembly == null)
        {
            return;
        }

        string warningKey = $"{modId}|{entry.Assembly.FullName}|{otherAssembly.FullName}";
        if (WarnedDuplicateModIds.TryAdd(warningKey, 0))
        {
            ModLogger.Warn(
                $"发现多个程序集使用相同 ModId {modId} 注册暂停菜单按钮，这可能影响排序和约定本地化键。",
                entry.Assembly);
        }
    }

    private static void OnModUnregistered(ModContext context)
    {
        UnregisterAssembly(context.Assembly);
    }
}
