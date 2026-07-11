using JmcModLib.Compat;
using JmcModLib.Multiplayer.Internal;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using System.Diagnostics.CodeAnalysis;

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
    /// 错误为 MOD 不匹配，且任一侧的缺失项中包含有效的 JML
    /// 可选网络功能兼容标记时返回 <see langword="true"/>。
    /// </returns>
    /// <remarks>
    /// 第三方 MOD 若也接管 MOD 不匹配弹窗，应在覆盖当前结果前调用此方法；
    /// 返回 <see langword="true"/> 时应保留 JML 已生成的提示。本方法只进行分类，
    /// 不创建 UI，也不修改网络功能状态。
    /// </remarks>
    public static bool ShouldHandle(NetErrorInfo info)
    {
        return TryGetHandledExtraInfo(info, out _);
    }

    internal static bool TryGetHandledExtraInfo(
        NetErrorInfo info,
        [NotNullWhen(true)] out ConnectionFailureExtraInfo? extraInfo)
    {
        extraInfo = null;
        if (info.GetReason() != NetError.ModMismatch
            || !MultiplayerCompat.TryGetConnectionExtraInfo(info, out extraInfo))
        {
            return false;
        }

        return ContainsOptionalFeatureToken(extraInfo.missingModsOnHost)
            || ContainsOptionalFeatureToken(extraInfo.missingModsOnLocal);
    }

    private static bool ContainsOptionalFeatureToken(IEnumerable<string>? entries)
    {
        return entries?.Any(static entry =>
            OptionalNetworkFeatureIdentity.TryParse(entry, out _)) == true;
    }
}
