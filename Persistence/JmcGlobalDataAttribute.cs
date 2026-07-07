namespace JmcModLib.Persistence;

/// <summary>
/// 将静态字段或静态属性注册为当前账号范围内的 JML 全局持久化数据。
/// </summary>
/// <remarks>
/// 全局数据不随游戏 profile 切换而变化，适合保存跨 profile 共享的缓存、统计或状态。
/// 成员可以是 <see cref="JmcDataSlot{T}"/>，也可以是简单裸静态值。
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class JmcGlobalDataAttribute : Attribute
{
    /// <summary>
    /// 创建全局持久化数据声明。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的数据键。</param>
    public JmcGlobalDataAttribute(string key)
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
