namespace JmcModLib.Persistence;

/// <summary>
/// 将静态 <see cref="JmcRunDataSlot{T}"/> 字段或属性注册为当前客户端、当前 run 生命周期内的数据。
/// </summary>
/// <remarks>
/// Client run data 写入本机 sidecar 文件，不进入游戏 run save，不使用云存档，不参与多人同步。
/// 它适合保存本局 UI 状态、本局提示已显示标记、本局诊断或预览状态等不应污染存档语义的数据。
/// 保存退出后可恢复，加载旧 run save 不会回滚；run 结束、放弃、删除或开启新 run 时会清理。
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class JmcClientRunDataAttribute : Attribute
{
    /// <summary>
    /// 创建客户端本局数据声明。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的数据键。</param>
    public JmcClientRunDataAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key.Trim();
    }

    /// <summary>
    /// 当前 MOD 内稳定的数据键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 数据结构版本；第一阶段仅写入 sidecar 文档，不自动执行迁移。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// 刷新时的写入策略。
    /// </summary>
    public JmcDataWritePolicy WritePolicy { get; set; } = JmcDataWritePolicy.WhenChanged;
}
