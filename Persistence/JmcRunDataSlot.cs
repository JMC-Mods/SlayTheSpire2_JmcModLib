namespace JmcModLib.Persistence;

/// <summary>
/// 子 MOD 用于读写当前 run 非同步持久化数据的槽位句柄。
/// </summary>
/// <typeparam name="T">槽位保存的数据类型。</typeparam>
/// <remarks>
/// 第一阶段 run data 只用于本地 run save 读写，不参与多人同步。引用类型数据发生内部变化时，
/// 应优先通过 <see cref="Modify(Action{T})"/> 包裹修改。
/// </remarks>
public sealed class JmcRunDataSlot<T>
{
    private IJmcRunDataSlotBinding<T>? binding;
    private T? value;

    /// <summary>
    /// 创建使用 <typeparamref name="T"/> 默认值的 run 槽位。
    /// </summary>
    public JmcRunDataSlot()
    {
        value = default;
    }

    /// <summary>
    /// 创建带初始默认值的 run 槽位。
    /// </summary>
    /// <param name="defaultValue">新 run 或存档中没有对应数据时使用的默认值。</param>
    public JmcRunDataSlot(T defaultValue)
    {
        value = defaultValue;
    }

    /// <summary>
    /// 当前槽位是否已经由 JML Attribute 扫描绑定。
    /// </summary>
    public bool IsBound => binding != null;

    /// <summary>
    /// 当前槽位绑定的数据键；未绑定时为空字符串。
    /// </summary>
    public string Key => binding?.Key ?? string.Empty;

    /// <summary>
    /// 当前 run 槽位值；不在 run 上下文中时返回类型默认值。
    /// </summary>
    public T Value
    {
        get
        {
            if (binding == null)
            {
                return value!;
            }

            return binding.CanAccess ? binding.GetValue() : default!;
        }
    }

    /// <summary>
    /// 设置当前 run 槽位值并标记为待写入 run save。
    /// </summary>
    /// <param name="newValue">新的槽位值。</param>
    /// <returns>写入请求结果。</returns>
    public JmcDataWriteResult SetValue(T newValue)
    {
        if (binding == null)
        {
            value = newValue;
            return JmcDataWriteResult.Failed("run 槽位尚未绑定，值只保存在当前内存中。");
        }

        if (!binding.CanAccess)
        {
            binding.LogRunContextUnavailable();
            return JmcDataWriteResult.Failed("当前没有可写入的 run 上下文。");
        }

        return binding.SetValue(newValue);
    }

    /// <summary>
    /// 修改当前 run 槽位值并标记为待写入 run save。
    /// </summary>
    /// <param name="update">对当前值执行的修改逻辑。</param>
    /// <returns>写入请求结果。</returns>
    public JmcDataWriteResult Modify(Action<T> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (binding != null && !binding.CanAccess)
        {
            binding.LogRunContextUnavailable();
            return JmcDataWriteResult.Failed("当前没有可写入的 run 上下文。");
        }

        T current = Value;
        update(current);
        return SetValue(current);
    }

    internal T GetLocalValue()
    {
        return value!;
    }

    internal void SetLocalValue(T newValue)
    {
        value = newValue;
    }

    internal void Bind(IJmcRunDataSlotBinding<T> nextBinding)
    {
        ArgumentNullException.ThrowIfNull(nextBinding);
        if (binding != null && !ReferenceEquals(binding, nextBinding))
        {
            throw new InvalidOperationException($"run 持久化槽位 {Key} 已绑定，不能重复绑定到 {nextBinding.Key}。");
        }

        binding = nextBinding;
        value = nextBinding.GetValue();
    }

    internal void Unbind(IJmcRunDataSlotBinding<T> oldBinding)
    {
        if (ReferenceEquals(binding, oldBinding))
        {
            value = binding.GetValue();
            binding = null;
        }
    }
}

internal interface IJmcRunDataSlotBinding<T> : IJmcDataSlotBinding<T>
{
    bool CanAccess { get; }

    void LogRunContextUnavailable();
}
