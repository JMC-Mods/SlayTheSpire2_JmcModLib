namespace JmcModLib.Persistence;

/// <summary>
/// 指定持久化数据在刷新时的写入策略。
/// </summary>
public enum JmcDataWritePolicy
{
    /// <summary>
    /// 仅当 JML 观察到数据与上次保存值不同时写入。
    /// </summary>
    WhenChanged = 0,

    /// <summary>
    /// 每次刷新时都重新写入当前值。
    /// </summary>
    Always = 1,
}
