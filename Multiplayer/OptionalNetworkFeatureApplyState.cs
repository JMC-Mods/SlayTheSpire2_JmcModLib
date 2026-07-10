namespace JmcModLib.Multiplayer;

/// <summary>
/// 表示可选网络功能的配置状态如何应用到当前运行时协议。
/// </summary>
public enum OptionalNetworkFeatureApplyState
{
    /// <summary>
    /// 配置状态已经应用到当前运行时协议。
    /// </summary>
    Applied,

    /// <summary>
    /// 配置已经保存，但正在等待当前网络活动完整结束后应用。
    /// </summary>
    PendingNetworkIdle,

    /// <summary>
    /// 运行时协议重建失败，需要通过重启游戏完成应用。
    /// </summary>
    RestartRequired
}
