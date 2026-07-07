namespace JmcModLib.Persistence;

/// <summary>
/// 将静态字段或静态属性注册为当前 run 范围内的 JML 非同步持久化数据。
/// </summary>
/// <remarks>
/// 第一阶段 run data 只承诺本地保存与读取，不参与多人同步、重连同步或一致性校验。
/// 成员可以是 <see cref="JmcRunDataSlot{T}"/>，也可以是简单裸静态值。
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class JmcRunDataAttribute : Attribute
{
    /// <summary>
    /// 创建 run 持久化数据声明。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的数据键。</param>
    public JmcRunDataAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key.Trim();
    }

    /// <summary>
    /// 当前 MOD 内稳定的数据键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 数据结构版本；第一阶段仅写入 run save 扩展文档，不自动执行迁移。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// 刷新时的写入策略。
    /// </summary>
    public JmcDataWritePolicy WritePolicy { get; set; } = JmcDataWritePolicy.WhenChanged;
}
