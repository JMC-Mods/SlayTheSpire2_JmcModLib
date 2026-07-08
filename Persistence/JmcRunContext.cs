using JmcModLib.Persistence.Run;

namespace JmcModLib.Persistence;

/// <summary>
/// 提供当前 run 的轻量上下文查询能力。
/// </summary>
/// <remarks>
/// <para>
/// 该入口用于辅助本地偏好做 run 级隔离，例如把 <see cref="JmcRunIdentity"/> 与
/// <see cref="JmcLocalPreferenceAttribute"/> 保存的数据一起写入，再在恢复时确认它仍属于当前这一局。
/// 它不是持久化入口，也不会替调用方写入或清理数据。
/// </para>
/// </remarks>
public static class JmcRunContext
{
    /// <summary>
    /// 尝试获取当前 run 的本地身份快照。
    /// </summary>
    /// <param name="identity">成功时返回当前 run 身份；失败时返回默认值。</param>
    /// <returns>当前存在可识别的 run 上下文时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    public static bool TryGetCurrentRunIdentity(out JmcRunIdentity identity)
    {
        return RunPersistenceManager.TryGetCurrentRunIdentity(out identity);
    }
}
