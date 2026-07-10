using Godot;
using JmcModLib.Compat;
using JmcModLib.Config;
using JmcModLib.Config.Entry;
using JmcModLib.Core.AttributeRouter;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using System.Reflection;
using AttributeRouting = JmcModLib.Core.AttributeRouter.AttributeRouter;

namespace JmcModLib.Multiplayer.Internal;

internal static class OptionalNetworkFeatureManager
{
    internal const string CompatibilityTokenPrefix = "JML-ONF1";

    private static readonly object Gate = new();
    private static readonly OptionalNetworkFeatureAttributeHandler AttributeHandler = new();
    private static readonly Dictionary<Assembly, List<PendingDeclaration>> PendingDeclarations = [];
    private static readonly Dictionary<Assembly, Dictionary<string, FeatureRegistration>> Features = [];
    private static readonly Dictionary<Assembly, OwnerState> Owners = [];
    private static readonly Dictionary<Type, FeatureRegistration> MessageOwners = [];
    private static readonly HashSet<Assembly> UnsafeAssemblies = [];
    private static readonly Dictionary<Assembly, ModManifest> UnsafeManifests = [];
    private static readonly HashSet<Assembly> RestartPromptedAssemblies = [];

    private static int initialized;
    private static int protocolInitialized;
    private static int applyQueued;

    internal static void Init()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return;
        }

        ConfigManager.Init();
        AttributeRouting.RegisterHandler<OptionalNetworkFeatureAttribute>(AttributeHandler);
        AttributeRouting.AssemblyScanned += OnAssemblyScanned;
        AttributeRouting.AssemblyUnscanned += OnAssemblyUnscanned;
        ConfigManager.ValueChanged += OnConfigValueChanged;
        ModLogger.Debug("可选网络功能管理器已初始化。");
    }

    internal static bool TryGet(
        Assembly assembly,
        string id,
        out OptionalNetworkFeatureHandle? handle)
    {
        Init();
        lock (Gate)
        {
            if (Features.TryGetValue(assembly, out Dictionary<string, FeatureRegistration>? byId)
                && byId.TryGetValue(id, out FeatureRegistration? registration))
            {
                handle = registration.Handle;
                return true;
            }
        }

        handle = null;
        return false;
    }

    internal static void PrepareInitialProtocol()
    {
        lock (Gate)
        {
            foreach (OwnerState owner in Owners.Values)
            {
                UpdateManifest(owner);
            }
        }
    }

    internal static void MarkProtocolInitialized()
    {
        Volatile.Write(ref protocolInitialized, 1);
    }

    internal static bool ShouldIncludeMessage(Type messageType)
    {
        lock (Gate)
        {
            if (UnsafeAssemblies.Contains(messageType.Assembly))
            {
                return true;
            }

            return !MessageOwners.TryGetValue(messageType, out FeatureRegistration? registration)
                || registration.EffectiveEnabled;
        }
    }

    internal static IReadOnlyList<string> GetCompatibilityTokens()
    {
        lock (Gate)
        {
            return [.. Features
                .Where(pair => !UnsafeAssemblies.Contains(pair.Key))
                .SelectMany(static pair => pair.Value.Values)
                .Where(static registration => registration.EffectiveEnabled)
                .Select(static registration => BuildCompatibilityToken(registration))
                .Order(StringComparer.Ordinal)];
        }
    }

    internal static bool TryGetFeatureDescriptor(
        OptionalNetworkFeatureIdentity identity,
        out OptionalNetworkFeatureDescriptor descriptor)
    {
        lock (Gate)
        {
            FeatureRegistration? registration = GetAllFeaturesUnsafe().FirstOrDefault(candidate =>
                string.Equals(candidate.Handle.ModId, identity.ModId, StringComparison.Ordinal)
                && string.Equals(candidate.Handle.Id, identity.FeatureId, StringComparison.Ordinal)
                && string.Equals(
                    candidate.Handle.CompatibilityVersion,
                    identity.CompatibilityVersion,
                    StringComparison.Ordinal));
            if (registration != null)
            {
                descriptor = new OptionalNetworkFeatureDescriptor(
                    identity,
                    ConfigLocalization.GetDisplayName(registration.ConfigEntry),
                    ModRegistry.GetVersion(registration.Assembly),
                    registration.EffectiveEnabled);
                return true;
            }
        }

        descriptor = default;
        return false;
    }

    internal static void OnNetworkBecameIdle()
    {
        ScheduleApply();
    }

    private static void RegisterDeclaration(
        Assembly assembly,
        MemberAccessor member,
        OptionalNetworkFeatureAttribute attribute)
    {
        lock (Gate)
        {
            if (!PendingDeclarations.TryGetValue(assembly, out List<PendingDeclaration>? declarations))
            {
                declarations = [];
                PendingDeclarations[assembly] = declarations;
            }

            declarations.Add(new PendingDeclaration(member, attribute));
        }
    }

    private static void OnAssemblyScanned(Assembly assembly)
    {
        List<PendingDeclaration>? declarations;
        lock (Gate)
        {
            if (!PendingDeclarations.Remove(assembly, out declarations) || declarations.Count == 0)
            {
                return;
            }
        }

        try
        {
            foreach (PendingDeclaration declaration in declarations)
            {
                RegisterFeature(assembly, declaration);
            }

            AuditMessageOwnership(assembly);
        }
        catch (Exception ex)
        {
            InvalidateAssembly(assembly, ex.Message);
            ModLogger.Error("可选网络功能声明无效，已回退为始终影响联机。", ex, assembly);
        }
    }

    private static void RegisterFeature(Assembly assembly, PendingDeclaration declaration)
    {
        if (Volatile.Read(ref protocolInitialized) == 1)
        {
            throw new InvalidOperationException("可选网络功能必须在游戏基础协议初始化前完成注册。");
        }

        MemberAccessor member = declaration.Member;
        OptionalNetworkFeatureAttribute attribute = declaration.Attribute;
        string id = NormalizeRequired(attribute.Id, nameof(attribute.Id));
        string compatibilityVersion = NormalizeRequired(
            attribute.CompatibilityVersion,
            nameof(attribute.CompatibilityVersion));

        if (!member.IsStatic || !member.CanRead || member.ValueType != typeof(bool))
        {
            throw new InvalidOperationException(
                $"{member.DeclaringType.FullName}.{member.Name} 必须是可读取的静态 bool 字段或属性。");
        }

        Type markerType = attribute.MessageMarkerType
            ?? throw new InvalidOperationException($"可选网络功能 {id} 未指定消息标记接口。");
        if (!markerType.IsInterface || !typeof(INetMessage).IsAssignableFrom(markerType))
        {
            throw new InvalidOperationException(
                $"可选网络功能 {id} 的标记类型 {markerType.FullName} 必须是继承 INetMessage 的接口。");
        }

        if (markerType.Assembly != assembly)
        {
            throw new InvalidOperationException($"可选网络功能 {id} 的消息标记接口必须定义在所属 MOD 程序集中。");
        }

        ConfigEntry? configEntry = ConfigManager.GetEntries(assembly).FirstOrDefault(entry =>
            entry.SourceDeclaringType == member.DeclaringType
            && string.Equals(entry.SourceMemberName, member.Name, StringComparison.Ordinal));
        if (configEntry == null || configEntry.ValueType != typeof(bool))
        {
            throw new InvalidOperationException(
                $"可选网络功能 {id} 的目标成员必须同时声明为 bool Config 配置项。");
        }

        string modId = ModRegistry.GetModId(assembly);
        Mod? mod = ModRuntime.FindModById(modId);
        ModManifest manifest = ModCompat.GetManifest(mod)
            ?? throw new InvalidOperationException($"无法找到可选网络功能 {id} 所属 MOD 的 manifest。");

        bool requestedEnabled = (bool)(member.GetValue(null)
            ?? throw new InvalidOperationException($"无法读取可选网络功能 {id} 的配置值。"));

        lock (Gate)
        {
            if (!Features.TryGetValue(assembly, out Dictionary<string, FeatureRegistration>? byId))
            {
                byId = new Dictionary<string, FeatureRegistration>(StringComparer.Ordinal);
                Features[assembly] = byId;
            }

            if (byId.ContainsKey(id))
            {
                throw new InvalidOperationException($"可选网络功能标识 {id} 在同一 MOD 内重复声明。");
            }

            if (!Owners.TryGetValue(assembly, out OwnerState? owner))
            {
                bool manifestAlreadyManaged = Owners.Values.Any(candidate =>
                    ReferenceEquals(candidate.Manifest, manifest));
                if (manifest.affectsGameplay && !manifestAlreadyManaged)
                {
                    throw new InvalidOperationException(
                        $"可选网络功能 {id} 要求 manifest 初始声明 affects_gameplay=false。");
                }

                owner = new OwnerState(assembly, manifest, baselineAffectsGameplay: false);
                Owners[assembly] = owner;
            }

            var handle = new OptionalNetworkFeatureHandle(
                id,
                modId,
                compatibilityVersion,
                assembly,
                requestedEnabled,
                requestedEnabled,
                OptionalNetworkFeatureApplyState.Applied);
            var registration = new FeatureRegistration(
                assembly,
                member,
                markerType,
                configEntry,
                handle,
                requestedEnabled,
                requestedEnabled);

            Type[] messageTypes = GetLoadableTypes(assembly)
                .Where(type => IsConcreteMessage(type) && markerType.IsAssignableFrom(type))
                .ToArray();
            if (messageTypes.Length == 0)
            {
                throw new InvalidOperationException($"可选网络功能 {id} 的标记接口没有匹配任何具体网络消息类型。");
            }

            foreach (Type messageType in messageTypes)
            {
                if (MessageOwners.TryGetValue(messageType, out FeatureRegistration? existing))
                {
                    throw new InvalidOperationException(
                        $"网络消息 {messageType.FullName} 同时属于可选功能 {existing.Handle.Id} 与 {id}。");
                }
            }

            byId.Add(id, registration);
            owner.Features.Add(registration);
            foreach (Type messageType in messageTypes)
            {
                MessageOwners.Add(messageType, registration);
            }

            UpdateManifest(owner);
            ModLogger.Info(
                $"已注册可选网络功能 {id}，消息数 {messageTypes.Length}，当前生效：{requestedEnabled}。",
                assembly);
        }
    }

    private static void AuditMessageOwnership(Assembly assembly)
    {
        lock (Gate)
        {
            Type[] unownedMessages = GetLoadableTypes(assembly)
                .Where(IsConcreteMessage)
                .Where(type => !MessageOwners.ContainsKey(type))
                .ToArray();
            if (unownedMessages.Length == 0)
            {
                return;
            }

            string names = string.Join(", ", unownedMessages.Select(static type => type.FullName));
            throw new InvalidOperationException(
                $"声明可选网络功能的 MOD 仍包含未归属消息：{names}。每个自定义 INetMessage 都必须归属且仅归属一个功能。");
        }
    }

    private static void OnConfigValueChanged(ConfigEntry entry, object? value)
    {
        if (value is not bool requestedEnabled)
        {
            return;
        }

        List<FeatureRegistration> matches;
        lock (Gate)
        {
            if (!Features.TryGetValue(entry.Assembly, out Dictionary<string, FeatureRegistration>? byId))
            {
                return;
            }

            matches = byId.Values.Where(registration =>
                registration.ConfigEntry.SourceDeclaringType == entry.SourceDeclaringType
                && string.Equals(
                    registration.ConfigEntry.SourceMemberName,
                    entry.SourceMemberName,
                    StringComparison.Ordinal)).ToList();

            foreach (FeatureRegistration registration in matches)
            {
                registration.RequestedEnabled = requestedEnabled;
                if (Volatile.Read(ref protocolInitialized) == 0)
                {
                    registration.EffectiveEnabled = requestedEnabled;
                    registration.Handle.UpdateRequested(requestedEnabled, OptionalNetworkFeatureApplyState.Applied);
                    registration.Handle.UpdateEffective(requestedEnabled, OptionalNetworkFeatureApplyState.Applied);
                    UpdateManifest(Owners[entry.Assembly]);
                    continue;
                }

                OptionalNetworkFeatureApplyState state = requestedEnabled == registration.EffectiveEnabled
                    ? OptionalNetworkFeatureApplyState.Applied
                    : OptionalNetworkFeatureApplyState.PendingNetworkIdle;
                registration.Handle.UpdateRequested(requestedEnabled, state);
                if (state == OptionalNetworkFeatureApplyState.Applied)
                {
                    RestartPromptedAssemblies.Remove(registration.Assembly);
                }
            }
        }

        if (Volatile.Read(ref protocolInitialized) == 1
            && matches.Any(static registration => registration.RequestedEnabled != registration.EffectiveEnabled))
        {
            ScheduleApply();
        }
    }

    private static void ScheduleApply()
    {
        if (Volatile.Read(ref protocolInitialized) == 0
            || Interlocked.Exchange(ref applyQueued, 1) == 1)
        {
            return;
        }

        Callable.From(TryApplyPending).CallDeferred();
    }

    private static void TryApplyPending()
    {
        Interlocked.Exchange(ref applyQueued, 0);
        if (!OptionalNetworkActivityTracker.IsTrackingReliable)
        {
            List<FeatureRegistration> restartRequired;
            lock (Gate)
            {
                restartRequired = GetAllFeaturesUnsafe()
                    .Where(static feature => feature.RequestedEnabled != feature.EffectiveEnabled)
                    .ToList();
            }

            foreach (FeatureRegistration registration in restartRequired)
            {
                registration.Handle.UpdateRequested(
                    registration.RequestedEnabled,
                    OptionalNetworkFeatureApplyState.RestartRequired);
            }

            if (restartRequired.Count > 0)
            {
                ModLogger.Error("无法可靠确认网络是否空闲，可选网络功能需要重启后应用。");
                PromptRestart(restartRequired.Select(static registration => registration.Assembly).Distinct());
            }

            return;
        }

        if (!OptionalNetworkActivityTracker.IsIdle)
        {
            lock (Gate)
            {
                foreach (FeatureRegistration registration in GetAllFeaturesUnsafe()
                             .Where(static feature => feature.RequestedEnabled != feature.EffectiveEnabled))
                {
                    registration.Handle.UpdateRequested(
                        registration.RequestedEnabled,
                        OptionalNetworkFeatureApplyState.PendingNetworkIdle);
                }
            }

            return;
        }

        List<FeatureRegistration> changed;
        Dictionary<FeatureRegistration, bool> previousStates;
        Exception? failure = null;
        lock (Gate)
        {
            changed = GetAllFeaturesUnsafe()
                .Where(static registration => registration.RequestedEnabled != registration.EffectiveEnabled)
                .ToList();
            if (changed.Count == 0)
            {
                return;
            }

            previousStates = changed.ToDictionary(
                static registration => registration,
                static registration => registration.EffectiveEnabled);
            foreach (FeatureRegistration registration in changed)
            {
                registration.EffectiveEnabled = registration.RequestedEnabled;
            }

            foreach (OwnerState owner in Owners.Values)
            {
                UpdateManifest(owner);
            }

            try
            {
                MessageTypes.Initialize();
            }
            catch (Exception ex)
            {
                failure = ex;
                foreach ((FeatureRegistration registration, bool previous) in previousStates)
                {
                    registration.EffectiveEnabled = previous;
                }

                foreach (OwnerState owner in Owners.Values)
                {
                    UpdateManifest(owner);
                }

                try
                {
                    MessageTypes.Initialize();
                }
                catch (Exception restoreException)
                {
                    ModLogger.Error("恢复旧网络消息表时也发生错误。", restoreException);
                }
            }
        }

        if (failure == null)
        {
            foreach (FeatureRegistration registration in changed)
            {
                registration.Handle.UpdateEffective(
                    registration.EffectiveEnabled,
                    OptionalNetworkFeatureApplyState.Applied);
                lock (Gate)
                {
                    RestartPromptedAssemblies.Remove(registration.Assembly);
                }

                ModLogger.Info(
                    $"可选网络功能 {registration.Handle.Id} 已热应用：{registration.EffectiveEnabled}。",
                    registration.Assembly);
            }

            return;
        }

        foreach (FeatureRegistration registration in changed)
        {
            registration.Handle.UpdateEffective(
                registration.EffectiveEnabled,
                OptionalNetworkFeatureApplyState.RestartRequired);
            ModLogger.Error(
                $"可选网络功能 {registration.Handle.Id} 热应用失败，将请求重启游戏。",
                failure,
                registration.Assembly);
        }

        PromptRestart(changed.Select(static registration => registration.Assembly).Distinct());
    }

    private static void PromptRestart(IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            lock (Gate)
            {
                if (!RestartPromptedAssemblies.Add(assembly))
                {
                    continue;
                }
            }

            _ = TaskHelper.RunSafely(GameRestart.ShowRestartConfirmationAsync(assembly: assembly));
        }
    }

    private static void InvalidateAssembly(Assembly assembly, string reason)
    {
        lock (Gate)
        {
            UnsafeAssemblies.Add(assembly);
            if (Features.Remove(assembly, out Dictionary<string, FeatureRegistration>? registrations))
            {
                foreach (FeatureRegistration registration in registrations.Values)
                {
                    foreach (Type messageType in MessageOwners
                                 .Where(pair => ReferenceEquals(pair.Value, registration))
                                 .Select(static pair => pair.Key)
                                 .ToArray())
                    {
                        MessageOwners.Remove(messageType);
                    }
                }
            }

            if (Owners.TryGetValue(assembly, out OwnerState? owner))
            {
                UnsafeManifests[assembly] = owner.Manifest;
                owner.Manifest.affectsGameplay = true;
            }
            else
            {
                string modId = ModRegistry.GetModId(assembly);
                Mod? mod = ModRuntime.FindModById(modId);
                ModManifest? manifest = ModCompat.GetManifest(mod);
                if (manifest != null)
                {
                    UnsafeManifests[assembly] = manifest;
                    manifest.affectsGameplay = true;
                }
            }
        }

        ModLogger.Error($"可选网络功能已进入安全回退：{reason}", assembly);
    }

    private static void OnAssemblyUnscanned(Assembly assembly)
    {
        lock (Gate)
        {
            PendingDeclarations.Remove(assembly);
            UnsafeAssemblies.Remove(assembly);
            UnsafeManifests.Remove(assembly);
            RestartPromptedAssemblies.Remove(assembly);
            if (Features.Remove(assembly, out Dictionary<string, FeatureRegistration>? registrations))
            {
                foreach (FeatureRegistration registration in registrations.Values)
                {
                    foreach (Type messageType in MessageOwners
                                 .Where(pair => ReferenceEquals(pair.Value, registration))
                                 .Select(static pair => pair.Key)
                                 .ToArray())
                    {
                        MessageOwners.Remove(messageType);
                    }
                }
            }

            Owners.Remove(assembly);
        }
    }

    private static void UpdateManifest(OwnerState owner)
    {
        owner.Manifest.affectsGameplay = UnsafeManifests.Values.Any(manifest =>
                ReferenceEquals(manifest, owner.Manifest))
            || Owners.Values
                .Where(candidate => ReferenceEquals(candidate.Manifest, owner.Manifest))
                .Any(static candidate => candidate.BaselineAffectsGameplay
                    || candidate.Features.Any(static registration => registration.EffectiveEnabled));
    }

    private static IEnumerable<FeatureRegistration> GetAllFeaturesUnsafe()
    {
        return Features.Values.SelectMany(static byId => byId.Values);
    }

    private static bool IsConcreteMessage(Type type)
    {
        return !type.IsAbstract
            && !type.IsInterface
            && !type.ContainsGenericParameters
            && typeof(INetMessage).IsAssignableFrom(type);
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static type => type != null).Cast<Type>().ToArray();
        }
    }

    private static string NormalizeRequired(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空。", name);
        }

        return value.Trim();
    }

    private static string BuildCompatibilityToken(FeatureRegistration registration)
    {
        return string.Join(
            ":",
            CompatibilityTokenPrefix,
            Uri.EscapeDataString(registration.Handle.ModId),
            Uri.EscapeDataString(registration.Handle.Id),
            Uri.EscapeDataString(registration.Handle.CompatibilityVersion));
    }

    private sealed record PendingDeclaration(
        MemberAccessor Member,
        OptionalNetworkFeatureAttribute Attribute);

    private sealed class FeatureRegistration(
        Assembly assembly,
        MemberAccessor member,
        Type markerType,
        ConfigEntry configEntry,
        OptionalNetworkFeatureHandle handle,
        bool requestedEnabled,
        bool effectiveEnabled)
    {
        internal Assembly Assembly { get; } = assembly;
        internal MemberAccessor Member { get; } = member;
        internal Type MarkerType { get; } = markerType;
        internal ConfigEntry ConfigEntry { get; } = configEntry;
        internal OptionalNetworkFeatureHandle Handle { get; } = handle;
        internal bool RequestedEnabled { get; set; } = requestedEnabled;
        internal bool EffectiveEnabled { get; set; } = effectiveEnabled;
    }

    private sealed class OwnerState(
        Assembly assembly,
        ModManifest manifest,
        bool baselineAffectsGameplay)
    {
        internal Assembly Assembly { get; } = assembly;
        internal ModManifest Manifest { get; } = manifest;
        internal bool BaselineAffectsGameplay { get; } = baselineAffectsGameplay;
        internal List<FeatureRegistration> Features { get; } = [];
    }

    private sealed class OptionalNetworkFeatureAttributeHandler : IAttributeHandler
    {
        public Action<Assembly, IReadOnlyList<ReflectionAccessorBase>>? Unregister => null;

        public void Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)
        {
            if (accessor is not MemberAccessor member
                || attribute is not OptionalNetworkFeatureAttribute featureAttribute)
            {
                throw new InvalidOperationException("OptionalNetworkFeature 只能声明在字段或属性上。");
            }

            RegisterDeclaration(assembly, member, featureAttribute);
        }
    }
}

internal readonly record struct OptionalNetworkFeatureDescriptor(
    OptionalNetworkFeatureIdentity Identity,
    string DisplayName,
    string? ModVersion,
    bool EffectiveEnabled);
