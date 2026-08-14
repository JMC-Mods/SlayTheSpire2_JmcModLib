using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace JmcModLib.Compat;

/// <summary>
/// 封装不同 STS2 版本中的多人错误信息与加入流程成员差异。
/// </summary>
public static class MultiplayerCompat
{
    // 0.99.1–0.107.1：NetErrorInfo 使用私有 readonly 字段 _connectionExtraInfo。
    // 0.108 起：该字段被移除，数据改由公开只读属性 ConnectionExtraInfo 保存；0.110 仍沿用该属性。
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

    // 0.99.1–0.109.1：ConnectionFailureExtraInfo 直接保存以房主/本机为语义的
    // missingModsOnHost / missingModsOnLocal 字段；0.110 删除了这两个字段。
    private static readonly Lazy<MemberAccessor?> MissingModsOnHostAccessor = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(ConnectionFailureExtraInfo),
            isStatic: false,
            "missingModsOnHost"));

    private static readonly Lazy<MemberAccessor?> MissingModsOnLocalAccessor = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(ConnectionFailureExtraInfo),
            isStatic: false,
            "missingModsOnLocal"));

    // 0.110 起：改为保存 localInfo / remoteInfo，并通过以下两个方法动态计算本机/对端差集。
    private static readonly Lazy<MethodAccessor?> GetMissingModsOnLocalMethod = new(() =>
        CompatMemberResolver.FindMethod(
            typeof(ConnectionFailureExtraInfo),
            isStatic: false,
            "GetMissingModsOnLocal",
            typeof(bool)));

    private static readonly Lazy<MethodAccessor?> GetMissingModsOnRemoteMethod = new(() =>
        CompatMemberResolver.FindMethod(
            typeof(ConnectionFailureExtraInfo),
            isStatic: false,
            "GetMissingModsOnRemote",
            typeof(bool)));

    // localInfo / remoteInfo 不再等价于 host / client；0.110 新增 localIsHost 用于恢复旧版的房主/本机语义。
    private static readonly Lazy<MemberAccessor?> LocalIsHostAccessor = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(ConnectionFailureExtraInfo),
            isStatic: false,
            "localIsHost"));

    // 0.99.1–0.109.1：RunLobby 公开 ConnectedPlayerIds；0.110 将玩家扩展为携带版本信息的
    // RunLobbyPlayer，并把纯 ID 视图改名为 PlayerIds。
    private static readonly Lazy<MemberAccessor?> RunLobbyPlayerIdsAccessor = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(RunLobby),
            isStatic: false,
            "PlayerIds",
            "ConnectedPlayerIds"));

    // 0.99.1–0.109.1：LoadRunLobby 公开可变 ConnectedPlayerIds；0.110 改为 Players 列表，
    // 并通过只读枚举 PlayerIds 暴露相同的玩家 ID 语义。
    private static readonly Lazy<MemberAccessor?> LoadRunLobbyPlayerIdsAccessor = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(LoadRunLobby),
            isStatic: false,
            "PlayerIds",
            "ConnectedPlayerIds"));

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
    /// 获取进行中多人局大厅当前记录的玩家 ID 快照。
    /// </summary>
    /// <param name="lobby">目标进行中大厅；为空时返回空集合。</param>
    /// <returns>当前大厅玩家 ID 的只读快照；当前游戏版本缺少可识别成员或读取失败时返回空集合。</returns>
    /// <remarks>
    /// 游戏 0.99.1–0.109.1 使用 <c>ConnectedPlayerIds</c>，0.110 将玩家记录扩展为
    /// <c>RunLobbyPlayer</c> 后改由 <c>PlayerIds</c> 提供相同的纯 ID 视图。
    /// 返回值是调用时复制的快照，修改它不会影响游戏大厅。
    /// </remarks>
    public static IReadOnlyList<ulong> GetRunLobbyPlayerIds(RunLobby? lobby)
    {
        return GetLobbyPlayerIds(lobby, RunLobbyPlayerIdsAccessor, "RunLobby");
    }

    /// <summary>
    /// 获取载入存档大厅当前记录的玩家 ID 快照。
    /// </summary>
    /// <param name="lobby">目标载入大厅；为空时返回空集合。</param>
    /// <returns>当前大厅玩家 ID 的只读快照；当前游戏版本缺少可识别成员或读取失败时返回空集合。</returns>
    /// <remarks>
    /// 游戏 0.99.1–0.109.1 使用 <c>ConnectedPlayerIds</c>，0.110 将玩家记录扩展为
    /// <c>LoadRunLobbyPlayer</c> 后改由 <c>PlayerIds</c> 提供相同的纯 ID 视图。
    /// 返回值是调用时复制的快照，修改它不会影响游戏大厅。
    /// </remarks>
    public static IReadOnlyList<ulong> GetLoadRunLobbyPlayerIds(LoadRunLobby? lobby)
    {
        return GetLobbyPlayerIds(lobby, LoadRunLobbyPlayerIdsAccessor, "LoadRunLobby");
    }

    /// <summary>
    /// 获取主机网络服务当前已完成连接的远端玩家 ID 快照。
    /// </summary>
    /// <param name="hostService">目标主机网络服务。</param>
    /// <returns>
    /// 当前已连接远端玩家 ID 的只读快照；当前游戏版本缺少可识别成员或读取失败时返回空集合。
    /// 返回值不包含本地主机 ID。
    /// </returns>
    /// <remarks>
    /// 游戏 0.107.1–0.110.1 通过 <c>INetHostGameService.ConnectedPeers</c> 暴露玩家，
    /// 0.111 将该属性从接口移除，但当前原生主机实现仍保留同名属性。调用方不应直接绑定
    /// 任一版本的接口布局。
    /// </remarks>
    public static IReadOnlyList<ulong> GetConnectedHostPeerIds(INetHostGameService hostService)
    {
        ArgumentNullException.ThrowIfNull(hostService);

        try
        {
            MemberAccessor? peersAccessor = CompatMemberResolver.FindReadableMember(
                hostService.GetType(),
                isStatic: false,
                "ConnectedPeers");
            if (peersAccessor?.GetValue(hostService) is not IEnumerable peers)
            {
                return [];
            }

            List<ulong> peerIds = [];
            foreach (object? peer in peers)
            {
                if (peer == null)
                {
                    continue;
                }

                MemberAccessor? peerIdAccessor = CompatMemberResolver.FindReadableMember(
                    peer.GetType(),
                    isStatic: false,
                    "peerId");
                if (peerIdAccessor?.GetValue(peer) is ulong peerId)
                {
                    peerIds.Add(peerId);
                }
            }

            return peerIds;
        }
        catch (Exception ex)
        {
            ModLogger.Warn("读取主机已连接玩家列表失败。", ex);
            return [];
        }
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

    internal static bool TryGetGameplayModMismatch(
        ConnectionFailureExtraInfo extraInfo,
        out List<string> missingOnHost,
        out List<string> missingOnLocal,
        out bool localIsHost)
    {
        ArgumentNullException.ThrowIfNull(extraInfo);

        missingOnHost = [];
        missingOnLocal = [];
        localIsHost = false;

        try
        {
            MethodAccessor? getMissingOnLocal = GetMissingModsOnLocalMethod.Value;
            MethodAccessor? getMissingOnRemote = GetMissingModsOnRemoteMethod.Value;
            if (getMissingOnLocal != null && getMissingOnRemote != null)
            {
                // 0.110 方法参数 nonGameplay=false 表示读取影响玩法的 MOD；JML 兼容令牌就在该集合中。
                List<string> missingOnCurrentPeer = ToStringList(
                    getMissingOnLocal.Invoke(extraInfo, false));
                List<string> missingOnRemotePeer = ToStringList(
                    getMissingOnRemote.Invoke(extraInfo, false));
                localIsHost = LocalIsHostAccessor.Value?.GetValue(extraInfo) is true;

                // 0.110 返回的是本机/对端方向，这里转换回 JML 对外一直使用的房主/本机方向。
                if (localIsHost)
                {
                    missingOnHost = missingOnCurrentPeer;
                    missingOnLocal = missingOnRemotePeer;
                }
                else
                {
                    missingOnHost = missingOnRemotePeer;
                    missingOnLocal = missingOnCurrentPeer;
                }

                return true;
            }

            // 0.107.1–0.109.1 没有上述方法，回退读取原有的 host/local 字段。
            MemberAccessor? missingOnHostAccessor = MissingModsOnHostAccessor.Value;
            MemberAccessor? missingOnLocalAccessor = MissingModsOnLocalAccessor.Value;
            if (missingOnHostAccessor == null || missingOnLocalAccessor == null)
            {
                return false;
            }

            missingOnHost = ToStringList(missingOnHostAccessor.GetValue(extraInfo));
            missingOnLocal = ToStringList(missingOnLocalAccessor.GetValue(extraInfo));
            return true;
        }
        catch (Exception ex)
        {
            missingOnHost = [];
            missingOnLocal = [];
            localIsHost = false;
            ModLogger.Warn("读取联机 MOD 不匹配列表失败。", ex);
            return false;
        }
    }

    private static List<string> ToStringList(object? value)
    {
        return value is IEnumerable<string> entries
            ? [.. entries.Where(static entry => !string.IsNullOrWhiteSpace(entry))]
            : [];
    }

    private static IReadOnlyList<ulong> GetLobbyPlayerIds<TLobby>(
        TLobby? lobby,
        Lazy<MemberAccessor?> lazyAccessor,
        string lobbyName)
        where TLobby : class
    {
        if (lobby == null)
        {
            return [];
        }

        try
        {
            MemberAccessor? accessor = lazyAccessor.Value;
            return accessor?.GetValue(lobby) is IEnumerable<ulong> playerIds
                ? [.. playerIds]
                : [];
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"读取 {lobbyName} 玩家 ID 列表失败。", ex);
            return [];
        }
    }
}
