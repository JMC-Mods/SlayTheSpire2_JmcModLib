namespace JmcModLib.Persistence;

/// <summary>
/// 将静态字段或静态属性注册为当前机器本地的 JML 客户端偏好数据。
/// </summary>
/// <remarks>
/// 本地偏好不进入游戏存档、不随 profile 切换、不参与云同步或多人同步，
/// 适合保存 UI 面板状态、排序方式、折叠状态、窗口位置和上次打开页签等不影响玩法结果的数据。
/// 成员可以是 <see cref="JmcDataSlot{T}"/>，也可以是简单裸静态值。
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class JmcLocalPreferenceAttribute : Attribute
{
    /// <summary>
    /// 创建本地偏好数据声明。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的数据键。</param>
    public JmcLocalPreferenceAttribute(string key)
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
