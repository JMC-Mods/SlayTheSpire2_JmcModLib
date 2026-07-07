using JmcModLib.Security.Backends;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace JmcModLib.Security;

/// <summary>
/// JML Secret 的统一读写入口。
/// </summary>
/// <remarks>
/// 普通子 MOD 优先持有 <see cref="JmcSecretSlot"/> 并通过槽位读取。此静态类用于需要按键动态访问的高级场景。
/// Secret 不会写入普通配置 JSON。
/// </remarks>
public static class JmcSecretStore
{
    /// <summary>
    /// 获取当前平台默认 Secret 后端的保护等级。
    /// </summary>
    /// <returns>默认后端保护等级。</returns>
    public static JmcSecretProtectionLevel GetProtectionLevel()
    {
        return GetProtectionLevel(allowWeakFileProtection: false);
    }

    /// <summary>
    /// 尝试读取指定 Secret。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的 Secret 键。</param>
    /// <param name="value">读取成功时返回 Secret 明文；失败时为空字符串。</param>
    /// <param name="status">读取结果状态。</param>
    /// <param name="scope">可选范围；用于区分同一键下的不同账号、服务商或环境。</param>
    /// <param name="assembly">Secret 所属程序集；留空时自动推断调用方程序集。</param>
    /// <returns>读取成功时返回 <see langword="true"/>。</returns>
    public static bool TryRead(
        string key,
        out string value,
        out JmcSecretReadStatus status,
        string? scope = null,
        Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcSecretStore));
        return TryRead(resolvedAssembly, key, scope, allowWeakFileProtection: false, out value, out status);
    }

    /// <summary>
    /// 尝试保存指定 Secret。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的 Secret 键。</param>
    /// <param name="value">要保存的 Secret 明文。</param>
    /// <param name="status">写入结果状态。</param>
    /// <param name="scope">可选范围；用于区分同一键下的不同账号、服务商或环境。</param>
    /// <param name="assembly">Secret 所属程序集；留空时自动推断调用方程序集。</param>
    /// <returns>保存成功时返回 <see langword="true"/>。</returns>
    public static bool TrySave(
        string key,
        string value,
        out JmcSecretWriteStatus status,
        string? scope = null,
        Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcSecretStore));
        return TrySave(resolvedAssembly, key, scope, value, allowWeakFileProtection: false, out status);
    }

    /// <summary>
    /// 尝试删除指定 Secret。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的 Secret 键。</param>
    /// <param name="status">删除结果状态。</param>
    /// <param name="scope">可选范围；用于区分同一键下的不同账号、服务商或环境。</param>
    /// <param name="assembly">Secret 所属程序集；留空时自动推断调用方程序集。</param>
    /// <returns>删除成功或目标已不存在时返回 <see langword="true"/>。</returns>
    public static bool TryDelete(
        string key,
        out JmcSecretWriteStatus status,
        string? scope = null,
        Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcSecretStore));
        return TryDelete(resolvedAssembly, key, scope, allowWeakFileProtection: false, out status);
    }

    /// <summary>
    /// 判断指定 Secret 是否已保存。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的 Secret 键。</param>
    /// <param name="scope">可选范围；用于区分同一键下的不同账号、服务商或环境。</param>
    /// <param name="assembly">Secret 所属程序集；留空时自动推断调用方程序集。</param>
    /// <returns>存在已保存 Secret 时返回 <see langword="true"/>。</returns>
    public static bool Exists(string key, string? scope = null, Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(JmcSecretStore));
        return Exists(resolvedAssembly, key, scope, allowWeakFileProtection: false);
    }

    internal static JmcSecretProtectionLevel GetProtectionLevel(bool allowWeakFileProtection)
    {
        return SecretBackendSelector.GetProtectionLevel(allowWeakFileProtection);
    }

    internal static bool TryRead(
        SecretRegistration registration,
        out string value,
        out JmcSecretReadStatus status)
    {
        return TryRead(
            registration.Assembly,
            registration.Key,
            registration.ResolveScope(),
            registration.AllowWeakFileProtection,
            out value,
            out status);
    }

    internal static bool TrySave(
        SecretRegistration registration,
        string value,
        out JmcSecretWriteStatus status)
    {
        return TrySave(
            registration.Assembly,
            registration.Key,
            registration.ResolveScope(),
            value,
            registration.AllowWeakFileProtection,
            out status);
    }

    internal static bool TryDelete(SecretRegistration registration, out JmcSecretWriteStatus status)
    {
        return TryDelete(
            registration.Assembly,
            registration.Key,
            registration.ResolveScope(),
            registration.AllowWeakFileProtection,
            out status);
    }

    internal static bool Exists(SecretRegistration registration)
    {
        return Exists(
            registration.Assembly,
            registration.Key,
            registration.ResolveScope(),
            registration.AllowWeakFileProtection);
    }

    private static bool TryRead(
        Assembly assembly,
        string key,
        string? scope,
        bool allowWeakFileProtection,
        out string value,
        out JmcSecretReadStatus status)
    {
        value = string.Empty;
        SecretIdentifier id = new(assembly, key, scope);
        ISecretBackend backend = SecretBackendSelector.Select(allowWeakFileProtection);
        if (!backend.TryRead(id, out byte[] bytes, out status))
        {
            return false;
        }

        try
        {
            value = Encoding.UTF8.GetString(bytes);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static bool TrySave(
        Assembly assembly,
        string key,
        string? scope,
        string value,
        bool allowWeakFileProtection,
        out JmcSecretWriteStatus status)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            status = JmcSecretWriteStatus.BackendError;
            return false;
        }

        SecretIdentifier id = new(assembly, key, scope);
        ISecretBackend backend = SecretBackendSelector.Select(allowWeakFileProtection);
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            return backend.TrySave(id, bytes, out status);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static bool TryDelete(
        Assembly assembly,
        string key,
        string? scope,
        bool allowWeakFileProtection,
        out JmcSecretWriteStatus status)
    {
        SecretIdentifier id = new(assembly, key, scope);
        return SecretBackendSelector.Select(allowWeakFileProtection).TryDelete(id, out status);
    }

    private static bool Exists(
        Assembly assembly,
        string key,
        string? scope,
        bool allowWeakFileProtection)
    {
        SecretIdentifier id = new(assembly, key, scope);
        return SecretBackendSelector.Select(allowWeakFileProtection).Exists(id);
    }
}
