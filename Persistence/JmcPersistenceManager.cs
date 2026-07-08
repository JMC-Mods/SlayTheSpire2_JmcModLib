using JmcModLib.Persistence.AttributeRouting;
using JmcModLib.Persistence.Entries;
using JmcModLib.Persistence.Run;
using JmcModLib.Persistence.Storage;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Saves;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Reflection;
using CoreAttributeRouter = JmcModLib.Core.AttributeRouter.AttributeRouter;

namespace JmcModLib.Persistence;

/// <summary>
/// JML Persistence 的统一初始化与刷新入口。
/// </summary>
/// <remarks>
/// 子 MOD 通常只需要声明 <see cref="JmcLocalPreferenceAttribute"/>、
/// <see cref="JmcGlobalDataAttribute"/>、<see cref="JmcProfileDataAttribute"/> 或 <see cref="JmcRunDataAttribute"/>。
/// 本类型主要用于需要立即写盘时手动调用 <see cref="Flush(Assembly?)"/>。
/// </remarks>
public static class JmcPersistenceManager
{
    private static readonly ConcurrentDictionary<Assembly, List<PersistenceEntry>> Entries = new();
    private static readonly NewtonsoftPersistenceStorage Storage = new();
    private static readonly PersistenceAttributeHandler LocalPreferenceHandler = new(PersistenceScope.LocalPreference);
    private static readonly PersistenceAttributeHandler GlobalHandler = new(PersistenceScope.Global);
    private static readonly PersistenceAttributeHandler ProfileHandler = new(PersistenceScope.Profile);
    private static readonly PersistenceAttributeHandler RunHandler = new(PersistenceScope.Run);

    private static int initialized;
    private static int profileSubscriptionState;

    /// <summary>
    /// 当前 Persistence 模块是否已经初始化。
    /// </summary>
    public static bool IsInitialized => Volatile.Read(ref initialized) == 1;

    /// <summary>
    /// 初始化 Persistence 模块并注册 Attribute 扫描处理器。
    /// </summary>
    public static void Init()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return;
        }

        CoreAttributeRouter.Init();
        CoreAttributeRouter.RegisterHandler<JmcLocalPreferenceAttribute>(LocalPreferenceHandler);
        CoreAttributeRouter.RegisterHandler<JmcGlobalDataAttribute>(GlobalHandler);
        CoreAttributeRouter.RegisterHandler<JmcProfileDataAttribute>(ProfileHandler);
        CoreAttributeRouter.RegisterHandler<JmcRunDataAttribute>(RunHandler);
        ModRegistry.OnUnregistered += OnModUnregistered;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        EnsureProfileSubscription();
        ModLogger.Debug("JmcPersistenceManager initialized.");
    }

    /// <summary>
    /// 释放 Persistence 模块持有的注册信息，并先刷新已注册的对局外数据。
    /// </summary>
    public static void Dispose()
    {
        if (Interlocked.Exchange(ref initialized, 0) == 0)
        {
            return;
        }

        FlushAll();
        ModRegistry.OnUnregistered -= OnModUnregistered;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        TryUnsubscribeProfileChanged();

        foreach (Assembly assembly in Entries.Keys.ToArray())
        {
            UnregisterAssembly(assembly, flushBeforeRemove: false);
        }

        _ = CoreAttributeRouter.UnregisterHandler(LocalPreferenceHandler);
        _ = CoreAttributeRouter.UnregisterHandler(GlobalHandler);
        _ = CoreAttributeRouter.UnregisterHandler(ProfileHandler);
        _ = CoreAttributeRouter.UnregisterHandler(RunHandler);
        ModLogger.Debug("JmcPersistenceManager disposed.");
    }

    /// <summary>
    /// 刷新当前调用方 MOD 的本地偏好、全局和 profile 数据。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动推断调用方程序集。</param>
    public static void Flush(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcPersistenceManager));
        FlushAssembly(resolvedAssembly, PersistenceScope.LocalPreference);
        FlushAssembly(resolvedAssembly, PersistenceScope.Global);
        FlushAssembly(resolvedAssembly, PersistenceScope.Profile);
    }

    /// <summary>
    /// 刷新当前调用方 MOD 的本地偏好数据。
    /// </summary>
    /// <param name="assembly">目标程序集；留空时自动推断调用方程序集。</param>
    public static void FlushLocalPreferences(Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcPersistenceManager));
        FlushAssembly(resolvedAssembly, PersistenceScope.LocalPreference);
    }

    /// <summary>
    /// 刷新所有已注册 MOD 的本地偏好、全局和 profile 数据。
    /// </summary>
    public static void FlushAll()
    {
        foreach (Assembly assembly in Entries.Keys.ToArray())
        {
            try
            {
                Flush(assembly);
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"刷新 Persistence 数据失败：{ModRegistry.GetTag(assembly)}", ex, assembly);
            }
        }
    }

    internal static void RegisterFromAttribute(
        Assembly assembly,
        MemberAccessor member,
        PersistenceScope scope,
        PersistenceAttributeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(member);
        Init();

        if (!member.IsStatic)
        {
            ModLogger.Error($"Persistence 只能声明在静态字段或属性上：{member.DeclaringType.FullName}.{member.Name}", assembly);
            return;
        }

        if (!member.CanRead)
        {
            ModLogger.Error($"Persistence 成员必须可读：{member.DeclaringType.FullName}.{member.Name}", assembly);
            return;
        }

        if (TryRegisterSlot(assembly, member, scope, descriptor))
        {
            return;
        }

        if (!member.CanWrite)
        {
            ModLogger.Error($"裸静态 Persistence 成员必须可写：{member.DeclaringType.FullName}.{member.Name}", assembly);
            return;
        }

        RegisterMemberEntry(assembly, member, scope, descriptor);
    }

    internal static void ReloadProfileData()
    {
        foreach (Assembly assembly in Entries.Keys.ToArray())
        {
            LoadAssemblyScope(assembly, PersistenceScope.Profile);
        }
    }

    internal static void FlushProfileData()
    {
        foreach (Assembly assembly in Entries.Keys.ToArray())
        {
            FlushAssembly(assembly, PersistenceScope.Profile);
        }
    }

    internal static void LoadRunEntries(RunPersistenceDocument document)
    {
        foreach (PersistenceEntry entry in GetEntriesByScope(PersistenceScope.Run))
        {
            JToken? token = document.GetValue(entry.ModId, entry.StorageKey);
            entry.InitializeFromToken(token);
        }
    }

    internal static void CaptureRunEntries(RunPersistenceDocument document)
    {
        foreach (PersistenceEntry entry in GetEntriesByScope(PersistenceScope.Run))
        {
            PersistenceDocumentEntry? documentEntry = entry.CaptureDocumentEntry();
            if (documentEntry != null)
            {
                document.SetValue(entry.ModId, entry.StorageKey, documentEntry);
            }
        }
    }

    internal static void ResetRunEntriesToDefault()
    {
        foreach (PersistenceEntry entry in GetEntriesByScope(PersistenceScope.Run))
        {
            entry.ResetToDefault();
        }
    }

    private static bool TryRegisterSlot(
        Assembly assembly,
        MemberAccessor member,
        PersistenceScope scope,
        PersistenceAttributeDescriptor descriptor)
    {
        Type? dataSlotType = TryGetGenericArgument(member.ValueType, typeof(JmcDataSlot<>));
        Type? runSlotType = TryGetGenericArgument(member.ValueType, typeof(JmcRunDataSlot<>));
        Type? expectedSlotType = scope == PersistenceScope.Run
            ? runSlotType
            : dataSlotType;
        if (expectedSlotType == null)
        {
            if (dataSlotType != null || runSlotType != null)
            {
                ModLogger.Error($"Persistence Slot 类型与 Attribute 范围不匹配：{member.DeclaringType.FullName}.{member.Name}", assembly);
                return true;
            }

            return false;
        }

        MethodInfo method = typeof(JmcPersistenceManager)
            .GetMethod(nameof(RegisterSlotEntry), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(expectedSlotType);
        _ = method.Invoke(null, [assembly, member, scope, descriptor]);
        return true;
    }

    private static void RegisterSlotEntry<T>(
        Assembly assembly,
        MemberAccessor member,
        PersistenceScope scope,
        PersistenceAttributeDescriptor descriptor)
    {
        object? slot = member.GetValue(null);
        if (slot == null)
        {
            if (!member.CanWrite)
            {
                ModLogger.Error($"Persistence Slot 为空且不可写，请初始化为 new：{member.DeclaringType.FullName}.{member.Name}", assembly);
                return;
            }

            slot = Activator.CreateInstance(member.ValueType);
            member.SetValue(null, slot);
        }

        if (slot == null)
        {
            ModLogger.Error($"无法创建 Persistence Slot：{member.DeclaringType.FullName}.{member.Name}", assembly);
            return;
        }

        JmcDataRegistration registration = CreateRegistration(assembly, member, scope, descriptor);
        PersistenceEntry entry;
        if (scope == PersistenceScope.Run)
        {
            entry = new PersistenceSlotEntry<T>(registration, (JmcRunDataSlot<T>)slot);
        }
        else
        {
            entry = new PersistenceSlotEntry<T>(registration, (JmcDataSlot<T>)slot);
        }

        RegisterEntry(entry);
    }

    private static void RegisterMemberEntry(
        Assembly assembly,
        MemberAccessor member,
        PersistenceScope scope,
        PersistenceAttributeDescriptor descriptor)
    {
        MethodInfo method = typeof(JmcPersistenceManager)
            .GetMethod(nameof(RegisterMemberEntryCore), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(member.ValueType);
        _ = method.Invoke(null, [assembly, member, scope, descriptor]);
    }

    private static void RegisterMemberEntryCore<T>(
        Assembly assembly,
        MemberAccessor member,
        PersistenceScope scope,
        PersistenceAttributeDescriptor descriptor)
    {
        JmcDataRegistration registration = CreateRegistration(assembly, member, scope, descriptor);
        RegisterEntry(new PersistenceMemberEntry<T>(registration, member));
    }

    private static JmcDataRegistration CreateRegistration(
        Assembly assembly,
        MemberAccessor member,
        PersistenceScope scope,
        PersistenceAttributeDescriptor descriptor)
    {
        return new JmcDataRegistration(
            assembly,
            scope,
            descriptor.Key,
            descriptor.SchemaVersion,
            descriptor.WritePolicy,
            member.DeclaringType,
            member.Name);
    }

    private static void RegisterEntry(PersistenceEntry entry)
    {
        List<PersistenceEntry> list = Entries.GetOrAdd(entry.Assembly, static _ => []);
        lock (list)
        {
            int existingIndex = list.FindIndex(existing =>
                existing.Scope == entry.Scope
                && string.Equals(existing.StorageKey, entry.StorageKey, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                PersistenceEntry existing = list[existingIndex];
                existing.Detach();
                list.RemoveAt(existingIndex);
                ModLogger.Warn($"Persistence key 重复注册，后注册项将覆盖前注册项：{entry.Scope}/{entry.Key}", entry.Assembly);
            }

            list.Add(entry);
        }

        LoadEntry(entry);
        ModLogger.Trace($"已注册 Persistence 数据：{entry.Scope}/{entry.Key}", entry.Assembly);
    }

    private static void LoadEntry(PersistenceEntry entry)
    {
        if (entry.Scope == PersistenceScope.Run)
        {
            RunPersistenceDocument? runDocument = RunPersistenceManager.CurrentDocument;
            entry.InitializeFromToken(runDocument?.GetValue(entry.ModId, entry.StorageKey));
            return;
        }

        if (!PersistencePathProvider.TryGetFilePath(entry.Scope, entry.Assembly, out string filePath))
        {
            return;
        }

        PersistenceDocument document = Storage.GetDocument(filePath, entry.Assembly);
        JToken? token = document.Values.TryGetValue(entry.StorageKey, out PersistenceDocumentEntry? value)
            ? value.Value
            : null;
        entry.InitializeFromToken(token);
    }

    private static void LoadAssemblyScope(Assembly assembly, PersistenceScope scope)
    {
        if (!Entries.TryGetValue(assembly, out List<PersistenceEntry>? list))
        {
            return;
        }

        PersistenceEntry[] snapshot;
        lock (list)
        {
            snapshot = [.. list.Where(entry => entry.Scope == scope)];
        }

        foreach (PersistenceEntry entry in snapshot)
        {
            LoadEntry(entry);
        }
    }

    private static void FlushAssembly(Assembly assembly, PersistenceScope scope)
    {
        if (!Entries.TryGetValue(assembly, out List<PersistenceEntry>? list))
        {
            return;
        }

        if (!PersistencePathProvider.TryGetFilePath(scope, assembly, out string filePath))
        {
            return;
        }

        PersistenceEntry[] snapshot;
        lock (list)
        {
            snapshot = [.. list.Where(entry => entry.Scope == scope)];
        }

        if (snapshot.Length == 0)
        {
            return;
        }

        PersistenceDocument document = Storage.GetDocument(filePath, assembly);
        bool changed = false;
        foreach (PersistenceEntry entry in snapshot)
        {
            PersistenceDocumentEntry? documentEntry = entry.CaptureDocumentEntry();
            if (documentEntry == null)
            {
                continue;
            }

            document.Values[entry.StorageKey] = documentEntry;
            changed = true;
        }

        if (changed)
        {
            Storage.MarkDirty(filePath);
            Storage.Flush(filePath, assembly);
        }
    }

    private static IEnumerable<PersistenceEntry> GetEntriesByScope(PersistenceScope scope)
    {
        foreach (List<PersistenceEntry> list in Entries.Values)
        {
            PersistenceEntry[] snapshot;
            lock (list)
            {
                snapshot = [.. list.Where(entry => entry.Scope == scope)];
            }

            foreach (PersistenceEntry entry in snapshot)
            {
                yield return entry;
            }
        }
    }

    private static void OnModUnregistered(ModContext context)
    {
        UnregisterAssembly(context.Assembly, flushBeforeRemove: true);
    }

    private static void UnregisterAssembly(Assembly assembly, bool flushBeforeRemove)
    {
        if (flushBeforeRemove)
        {
            Flush(assembly);
        }

        if (!Entries.TryRemove(assembly, out List<PersistenceEntry>? list))
        {
            return;
        }

        lock (list)
        {
            foreach (PersistenceEntry entry in list)
            {
                entry.Detach();
            }
        }
    }

    private static void OnProcessExit(object? sender, EventArgs args)
    {
        try
        {
            FlushAll();
        }
        catch
        {
            // 进程退出时忽略写盘失败，避免遮蔽原始退出流程。
        }
    }

    private static void EnsureProfileSubscription()
    {
        if (Interlocked.Exchange(ref profileSubscriptionState, 1) == 1)
        {
            return;
        }

        try
        {
            SaveManager.Instance.ProfileIdChanged += OnProfileIdChanged;
        }
        catch (Exception ex)
        {
            Volatile.Write(ref profileSubscriptionState, 0);
            ModLogger.Warn("订阅 SaveManager.ProfileIdChanged 失败，稍后将依赖 Harmony 生命周期补丁。", ex);
        }
    }

    private static void TryUnsubscribeProfileChanged()
    {
        if (Interlocked.Exchange(ref profileSubscriptionState, 0) == 0)
        {
            return;
        }

        try
        {
            SaveManager.Instance.ProfileIdChanged -= OnProfileIdChanged;
        }
        catch
        {
        }
    }

    private static void OnProfileIdChanged(int profileId)
    {
        ReloadProfileData();
    }

    private static Type? TryGetGenericArgument(Type valueType, Type genericTypeDefinition)
    {
        return valueType.IsGenericType && valueType.GetGenericTypeDefinition() == genericTypeDefinition
            ? valueType.GetGenericArguments()[0]
            : null;
    }
}
