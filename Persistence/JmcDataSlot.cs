namespace JmcModLib.Persistence;

/// <summary>
/// 子 MOD 用于读写全局或 profile 持久化数据的槽位句柄。
/// </summary>
/// <typeparam name="T">槽位保存的数据类型。</typeparam>
/// <remarks>
/// 引用类型数据发生内部变化时，应优先通过 <see cref="Modify(Action{T})"/> 包裹修改，
/// 不要依赖直接修改 <see cref="Value"/> 返回对象后自动保存。
/// </remarks>
public sealed class JmcDataSlot<T>
{
    private IJmcDataSlotBinding<T>? binding;
    private T? value;

    /// <summary>
    /// 创建使用 <typeparamref name="T"/> 默认值的槽位。
    /// </summary>
    public JmcDataSlot()
    {
        value = default;
    }

    /// <summary>
    /// 创建带初始默认值的槽位。
    /// </summary>
    /// <param name="defaultValue">存储文件中没有对应数据时使用的默认值。</param>
    public JmcDataSlot(T defaultValue)
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
    /// 当前槽位值。
    /// </summary>
    public T Value => binding == null ? value! : binding.GetValue();

    /// <summary>
    /// 设置槽位值并标记为待保存。
    /// </summary>
    /// <param name="newValue">新的槽位值。</param>
    /// <returns>写入请求结果。</returns>
    public JmcDataWriteResult SetValue(T newValue)
    {
        if (binding == null)
        {
            value = newValue;
            return JmcDataWriteResult.Failed("槽位尚未绑定，值只保存在当前内存中。");
        }

        return binding.SetValue(newValue);
    }

    /// <summary>
    /// 修改当前槽位值并标记为待保存。
    /// </summary>
    /// <param name="update">对当前值执行的修改逻辑。</param>
    /// <returns>写入请求结果。</returns>
    public JmcDataWriteResult Modify(Action<T> update)
    {
        ArgumentNullException.ThrowIfNull(update);

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

    internal void Bind(IJmcDataSlotBinding<T> nextBinding)
    {
        ArgumentNullException.ThrowIfNull(nextBinding);
        if (binding != null && !ReferenceEquals(binding, nextBinding))
        {
            throw new InvalidOperationException($"持久化槽位 {Key} 已绑定，不能重复绑定到 {nextBinding.Key}。");
        }

        binding = nextBinding;
        value = nextBinding.GetValue();
    }

    internal void Unbind(IJmcDataSlotBinding<T> oldBinding)
    {
        if (ReferenceEquals(binding, oldBinding))
        {
            value = binding.GetValue();
            binding = null;
        }
    }
}

internal interface IJmcDataSlotBinding<T>
{
    string Key { get; }

    T GetValue();

    JmcDataWriteResult SetValue(T value);
}
