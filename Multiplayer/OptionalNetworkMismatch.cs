using JmcModLib.Compat;
using JmcModLib.Multiplayer.Internal;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

namespace JmcModLib.Multiplayer;

/// <summary>
/// 提供 JML 可选网络功能不匹配错误的路由判断。
/// </summary>
public static class OptionalNetworkMismatch
{
    /// <summary>
    /// 判断指定联机错误是否应优先交由 JML 的可选网络功能提示处理。
    /// </summary>
    /// <param name="info">游戏联机错误信息。</param>
    /// <returns>
    /// 错误为 MOD 不匹配、任一侧的缺失项中包含有效的 JML
    /// 可选网络功能兼容标记，且当前客户端应显示 JML 专用提示时返回
    /// <see langword="true"/>。游戏 0.110 起本机为房主时返回
    /// <see langword="false"/>，由游戏原生提示处理。
    /// </returns>
    /// <remarks>
    /// 第三方 MOD 若也接管 MOD 不匹配弹窗，应在覆盖当前结果前调用此方法；
    /// 返回 <see langword="true"/> 时应保留 JML 已生成的提示。本方法只进行分类，
    /// 不创建 UI，也不修改网络功能状态。旧游戏版本不会向房主提供该错误详情，
    /// 因此不受房主侧回退行为影响。
    /// </remarks>
    public static bool ShouldHandle(NetErrorInfo info)
    {
        return TryGetHandledMismatch(info, out _, out _, out bool localIsHost)
            && !localIsHost;
    }

    internal static bool TryGetHandledMismatch(
        NetErrorInfo info,
        out List<string> missingOnHost,
        out List<string> missingOnLocal,
        out bool localIsHost)
    {
        missingOnHost = [];
        missingOnLocal = [];
        localIsHost = false;
        if (info.GetReason() != NetError.ModMismatch
            || !MultiplayerCompat.TryGetConnectionExtraInfo(
                info,
                out ConnectionFailureExtraInfo? extraInfo))
        {
            return false;
        }

        if (!MultiplayerCompat.TryGetGameplayModMismatch(
                extraInfo,
                out missingOnHost,
                out missingOnLocal,
                out localIsHost))
        {
            return false;
        }

        return ContainsOptionalFeatureToken(missingOnHost)
            || ContainsOptionalFeatureToken(missingOnLocal);
    }

    private static bool ContainsOptionalFeatureToken(IEnumerable<string>? entries)
    {
        return entries?.Any(static entry =>
            OptionalNetworkFeatureIdentity.TryParse(entry, out _)) == true;
    }
}
