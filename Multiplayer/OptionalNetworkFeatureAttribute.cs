namespace JmcModLib.Multiplayer;

/// <summary>
/// 将一个静态布尔配置声明为可选网络功能，并指定该功能独占的网络消息标记接口。
/// </summary>
/// <remarks>
/// <para>目标成员必须同时使用 <c>ConfigAttribute</c> 注册为静态 <see cref="bool"/> 配置。</para>
/// <para>
/// <paramref name="messageMarkerType"/> 必须是继承游戏 <c>INetMessage</c> 的接口，且只能标记当前功能拥有的消息类型。
/// 所属 MOD 的 manifest 初始值必须声明 <c>affects_gameplay=false</c>。
/// </para>
/// <para>声明必须在常规 MOD 初始化阶段通过 <c>ModRegistry.Register</c> 完成扫描，不能在游戏基础协议初始化后延迟注册。</para>
/// </remarks>
/// <param name="id">功能在所属 MOD 内的稳定标识。</param>
/// <param name="messageMarkerType">继承游戏 <c>INetMessage</c>、且由该功能独占的消息标记接口。</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OptionalNetworkFeatureAttribute(string id, Type messageMarkerType) : Attribute
{
    /// <summary>
    /// 获取功能在所属 MOD 内的稳定标识。
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// 获取该功能独占的网络消息标记接口。
    /// </summary>
    public Type MessageMarkerType { get; } = messageMarkerType;

    /// <summary>
    /// 获取或设置网络协议兼容版本；消息布局或交互流程发生不兼容变化时应递增。
    /// </summary>
    public string CompatibilityVersion { get; set; } = "1";
}
