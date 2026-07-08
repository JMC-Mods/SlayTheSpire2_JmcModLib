namespace JmcModLib.Persistence;

/// <summary>
/// 表示当前 run 的本地身份快照，可用于判断一条本地偏好是否仍属于当前这一局。
/// </summary>
/// <remarks>
/// <para>
/// 该类型只用于本机本地隔离判断，不会参与多人同步，也不代表 JML 会替调用方保存任何数据。
/// 常见用法是在 <see cref="JmcLocalPreferenceAttribute"/> 数据中保存此值，恢复时与
/// <see cref="JmcRunContext.TryGetCurrentRunIdentity(out JmcRunIdentity)"/> 的结果比较。
/// </para>
/// </remarks>
public readonly record struct JmcRunIdentity
{
    /// <summary>
    /// 创建一个当前 run 身份快照。
    /// </summary>
    /// <param name="profileId">当前游戏 profile ID。</param>
    /// <param name="startTime">当前 run 的开始时间戳，单位为 Unix 秒。</param>
    /// <param name="isMultiplayer">当前 run 是否为多人 run。</param>
    public JmcRunIdentity(int profileId, long startTime, bool isMultiplayer)
    {
        ProfileId = profileId;
        StartTime = startTime;
        IsMultiplayer = isMultiplayer;
    }

    /// <summary>
    /// 当前游戏 profile ID。
    /// </summary>
    public int ProfileId { get; init; }

    /// <summary>
    /// 当前 run 的开始时间戳，单位为 Unix 秒。
    /// </summary>
    public long StartTime { get; init; }

    /// <summary>
    /// 当前 run 是否为多人 run。
    /// </summary>
    public bool IsMultiplayer { get; init; }
}
