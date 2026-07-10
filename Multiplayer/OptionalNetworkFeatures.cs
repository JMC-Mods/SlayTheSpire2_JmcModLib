using JmcModLib.Multiplayer.Internal;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace JmcModLib.Multiplayer;

/// <summary>
/// 提供可选网络功能运行时句柄的查询入口。
/// </summary>
public static class OptionalNetworkFeatures
{
    /// <summary>
    /// 获取指定程序集已声明的可选网络功能句柄。
    /// </summary>
    /// <param name="id">功能在所属 MOD 内的稳定标识。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    /// <returns>对应的运行时句柄。</returns>
    /// <exception cref="KeyNotFoundException">功能尚未注册、声明无效或标识不存在。</exception>
    public static OptionalNetworkFeatureHandle Get(string id, Assembly? assembly = null)
    {
        if (TryGet(id, out OptionalNetworkFeatureHandle? handle, assembly))
        {
            return handle;
        }

        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(OptionalNetworkFeatures));
        throw new KeyNotFoundException(
            $"程序集 {resolvedAssembly.GetName().Name} 未注册可选网络功能 {id}，或该功能声明无效。");
    }

    /// <summary>
    /// 使用类型所在程序集获取已声明的可选网络功能句柄。
    /// </summary>
    /// <typeparam name="TOwner">位于目标 MOD 程序集中的非静态类型，通常使用入口 <c>MainFile</c>。</typeparam>
    /// <param name="id">功能在所属 MOD 内的稳定标识。</param>
    /// <returns>对应的运行时句柄。</returns>
    /// <exception cref="KeyNotFoundException">功能尚未注册、声明无效或标识不存在。</exception>
    public static OptionalNetworkFeatureHandle Get<TOwner>(string id)
    {
        return Get(id, typeof(TOwner).Assembly);
    }

    /// <summary>
    /// 尝试获取指定程序集已声明的可选网络功能句柄。
    /// </summary>
    /// <param name="id">功能在所属 MOD 内的稳定标识。</param>
    /// <param name="handle">成功时返回对应的运行时句柄。</param>
    /// <param name="assembly">所属程序集；留空时自动解析调用方程序集。</param>
    /// <returns>找到有效声明时返回 <see langword="true"/>。</returns>
    public static bool TryGet(
        string id,
        [NotNullWhen(true)] out OptionalNetworkFeatureHandle? handle,
        Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(OptionalNetworkFeatures));
        return OptionalNetworkFeatureManager.TryGet(resolvedAssembly, id.Trim(), out handle);
    }
}
