namespace JmcModLib.Persistence;

/// <summary>
/// 将静态字段或静态属性注册为当前 profile 范围内的 JML 持久化数据。
/// </summary>
/// <remarks>
/// Profile 数据会随游戏 profile 切换而重新加载，适合保存角色外统计、进度和缓存。
/// 成员可以是 <see cref="JmcDataSlot{T}"/>，也可以是简单裸静态值。
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class JmcProfileDataAttribute : Attribute
{
    /// <summary>
    /// 创建 profile 持久化数据声明。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的数据键。</param>
    public JmcProfileDataAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key.Trim();
    }

    /// <summary>
    /// 当前 MOD 内稳定的数据键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 数据结构版本；第一阶段仅写入文档，不自动执行迁移。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// 刷新时的写入策略。
    /// </summary>
    public JmcDataWritePolicy WritePolicy { get; set; } = JmcDataWritePolicy.WhenChanged;
}
