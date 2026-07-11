using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using System.Diagnostics.CodeAnalysis;

namespace JmcModLib.Compat;

/// <summary>
/// 封装不同 STS2 版本中的多人错误信息与加入流程成员差异。
/// </summary>
public static class MultiplayerCompat
{
    // 0.99.1–0.107.1：NetErrorInfo 使用私有 readonly 字段 _connectionExtraInfo。
    // 0.108：该字段被移除，数据改由公开只读属性 ConnectionExtraInfo 保存。
    private static readonly Lazy<MemberAccessor?> ConnectionExtraInfoAccessor = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(NetErrorInfo),
            isStatic: false,
            "ConnectionExtraInfo",
            "_connectionExtraInfo"));

    // 0.99.1–0.107.1：NetService 为 NetClientGameService? 属性，由 Begin() 内部创建。
    // 0.108：改为构造函数注入的 INetClientGameService 只读属性；JML 统一返回其父接口 INetGameService。
    // _netService 仅是对可能的私有字段化构建的防御性回退，当前归档 DLL 未使用它。
    private static readonly Lazy<MemberAccessor?> JoinFlowNetServiceAccessor = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(JoinFlow),
            isStatic: false,
            "NetService",
            "_netService"));

    /// <summary>
    /// 尝试读取联机错误携带的 MOD 不匹配等附加信息。
    /// </summary>
    /// <param name="info">游戏联机错误信息。</param>
    /// <param name="extraInfo">成功时返回附加信息。</param>
    /// <returns>当前游戏版本存在可识别成员且附加信息非空时返回 <see langword="true"/>。</returns>
    /// <remarks>
    /// 游戏 0.99.1–0.107.1 使用私有 <c>_connectionExtraInfo</c> 字段，0.108 起使用公开
    /// <c>ConnectionExtraInfo</c> 属性。调用方不应自行依赖这些版本成员名称。
    /// </remarks>
    public static bool TryGetConnectionExtraInfo(
        NetErrorInfo info,
        [NotNullWhen(true)] out ConnectionFailureExtraInfo? extraInfo)
    {
        try
        {
            MemberAccessor? accessor = ConnectionExtraInfoAccessor.Value;
            extraInfo = accessor?.GetValue<NetErrorInfo, ConnectionFailureExtraInfo?>(info);
            return extraInfo != null;
        }
        catch (Exception ex)
        {
            extraInfo = null;
            ModLogger.Warn("读取联机错误附加信息失败。", ex);
            return false;
        }
    }

    /// <summary>
    /// 尝试读取加入流程当前使用的网络服务。
    /// </summary>
    /// <param name="flow">游戏加入流程实例。</param>
    /// <param name="service">成功时返回网络服务。</param>
    /// <returns>当前游戏版本存在可识别成员且网络服务非空时返回 <see langword="true"/>。</returns>
    /// <remarks>
    /// 游戏 0.99.1–0.107.1 返回可空的具体类型 <c>NetClientGameService</c>；0.108 改为构造函数
    /// 注入的 <c>INetClientGameService</c>。本方法以两者共同的 <see cref="INetGameService"/>
    /// 对外暴露稳定语义。0.107.1 在 <c>JoinFlow.Begin()</c> 内部才创建服务，
    /// 因此在 <c>Begin()</c> 执行前调用本方法会返回 <see langword="false"/>，
    /// 但这不表示当前游戏版本缺少该成员。
    /// </remarks>
    public static bool TryGetJoinFlowNetService(
        JoinFlow flow,
        [NotNullWhen(true)] out INetGameService? service)
    {
        return TryReadJoinFlowNetService(flow, out service) && service != null;
    }

    /// <summary>
    /// 尝试读取 JoinFlow 的网络服务成员，并区分“成员缺失”与“成员尚未初始化”。
    /// </summary>
    /// <returns>成员存在且读取成功时返回 <see langword="true"/>；此时 <paramref name="service"/> 仍可为空。</returns>
    internal static bool TryReadJoinFlowNetService(JoinFlow flow, out INetGameService? service)
    {
        ArgumentNullException.ThrowIfNull(flow);

        try
        {
            MemberAccessor? accessor = JoinFlowNetServiceAccessor.Value;
            if (accessor == null)
            {
                service = null;
                return false;
            }

            service = accessor.GetValue<JoinFlow, INetGameService?>(flow);
            return true;
        }
        catch (Exception ex)
        {
            service = null;
            ModLogger.Warn("读取 JoinFlow 网络服务失败。", ex);
            return false;
        }
    }
}
