namespace JmcModLib.Multiplayer;

/// <summary>
/// 提供可选网络功能的配置意图、当前生效状态和应用进度。
/// </summary>
public sealed class OptionalNetworkFeatureHandle
{
    private readonly object syncRoot = new();
    private readonly System.Reflection.Assembly ownerAssembly;
    private bool requestedEnabled;
    private bool effectiveEnabled;
    private OptionalNetworkFeatureApplyState applyState;

    internal OptionalNetworkFeatureHandle(
        string id,
        string modId,
        string compatibilityVersion,
        System.Reflection.Assembly ownerAssembly,
        bool requestedEnabled,
        bool effectiveEnabled,
        OptionalNetworkFeatureApplyState applyState)
    {
        Id = id;
        ModId = modId;
        CompatibilityVersion = compatibilityVersion;
        this.ownerAssembly = ownerAssembly;
        this.requestedEnabled = requestedEnabled;
        this.effectiveEnabled = effectiveEnabled;
        this.applyState = applyState;
    }

    /// <summary>
    /// 获取功能在所属 MOD 内的稳定标识。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取所属 MOD 的稳定标识。
    /// </summary>
    public string ModId { get; }

    /// <summary>
    /// 获取参与多人兼容性校验的网络协议兼容版本。
    /// </summary>
    public string CompatibilityVersion { get; }

    /// <summary>
    /// 获取用户配置所请求的状态；该值可能尚未应用到当前网络协议。
    /// </summary>
    public bool RequestedEnabled
    {
        get
        {
            lock (syncRoot)
            {
                return requestedEnabled;
            }
        }
    }

    /// <summary>
    /// 获取当前运行时协议真正生效的状态；网络消息注册、发送和业务入口必须以此值为准。
    /// </summary>
    public bool EffectiveEnabled
    {
        get
        {
            lock (syncRoot)
            {
                return effectiveEnabled;
            }
        }
    }

    /// <summary>
    /// 获取当前配置的应用状态。
    /// </summary>
    public OptionalNetworkFeatureApplyState ApplyState
    {
        get
        {
            lock (syncRoot)
            {
                return applyState;
            }
        }
    }

    /// <summary>
    /// 获取是否仍有尚未应用到运行时协议的配置变化。
    /// </summary>
    public bool HasPendingApply
    {
        get
        {
            lock (syncRoot)
            {
                return requestedEnabled != effectiveEnabled
                    || applyState != OptionalNetworkFeatureApplyState.Applied;
            }
        }
    }

    /// <summary>
    /// 当任意公开状态发生变化时触发。
    /// </summary>
    public event Action<OptionalNetworkFeatureHandle>? StateChanged;

    /// <summary>
    /// 当当前运行时协议真正生效的启用状态发生变化时触发。
    /// </summary>
    public event Action<OptionalNetworkFeatureHandle>? EffectiveEnabledChanged;

    internal void UpdateRequested(bool enabled, OptionalNetworkFeatureApplyState state)
    {
        bool changed;
        lock (syncRoot)
        {
            changed = requestedEnabled != enabled || applyState != state;
            requestedEnabled = enabled;
            applyState = state;
        }

        if (changed)
        {
            InvokeSafely(StateChanged, nameof(StateChanged));
        }
    }

    internal void UpdateEffective(bool enabled, OptionalNetworkFeatureApplyState state)
    {
        bool stateChanged;
        bool effectiveChanged;
        lock (syncRoot)
        {
            effectiveChanged = effectiveEnabled != enabled;
            stateChanged = effectiveChanged || applyState != state;
            effectiveEnabled = enabled;
            applyState = state;
        }

        if (stateChanged)
        {
            InvokeSafely(StateChanged, nameof(StateChanged));
        }

        if (effectiveChanged)
        {
            InvokeSafely(EffectiveEnabledChanged, nameof(EffectiveEnabledChanged));
        }
    }

    private void InvokeSafely(
        Action<OptionalNetworkFeatureHandle>? handlers,
        string eventName)
    {
        if (handlers == null)
        {
            return;
        }

        foreach (Action<OptionalNetworkFeatureHandle> handler in handlers.GetInvocationList()
                     .Cast<Action<OptionalNetworkFeatureHandle>>())
        {
            try
            {
                handler(this);
            }
            catch (Exception ex)
            {
                ModLogger.Error($"可选网络功能 {Id} 的 {eventName} 事件处理器执行失败。", ex, ownerAssembly);
            }
        }
    }
}
