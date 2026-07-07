namespace JmcModLib.Security;

/// <summary>
/// 表示当前 Secret 后端能够提供的保护等级。
/// </summary>
public enum JmcSecretProtectionLevel
{
    /// <summary>
    /// 尚未确定保护等级。
    /// </summary>
    Unknown,

    /// <summary>
    /// 由操作系统钥匙串或同等级安全设施保护。
    /// </summary>
    SystemKeychain,

    /// <summary>
    /// 由当前用户配置文件保护，例如 Windows current-user DPAPI。
    /// </summary>
    UserProfileProtected,

    /// <summary>
    /// 仅使用受限权限文件保存；这不是安全加密。
    /// </summary>
    WeakFileProtection,

    /// <summary>
    /// 仅在当前进程会话内保留，不会持久化。
    /// </summary>
    SessionOnly,

    /// <summary>
    /// 当前平台或运行环境不支持保存 Secret。
    /// </summary>
    Unavailable
}

/// <summary>
/// 表示读取 Secret 的结果状态。
/// </summary>
public enum JmcSecretReadStatus
{
    /// <summary>
    /// 读取成功。
    /// </summary>
    Success,

    /// <summary>
    /// 尚未保存对应 Secret。
    /// </summary>
    Missing,

    /// <summary>
    /// 当前平台或运行环境不支持读取 Secret。
    /// </summary>
    Unavailable,

    /// <summary>
    /// 当前用户无权读取 Secret。
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Secret 密文无法解密，通常需要重新设置。
    /// </summary>
    DecryptionFailed,

    /// <summary>
    /// 后端发生其它错误。
    /// </summary>
    BackendError
}

/// <summary>
/// 表示写入或删除 Secret 的结果状态。
/// </summary>
public enum JmcSecretWriteStatus
{
    /// <summary>
    /// 操作成功。
    /// </summary>
    Success,

    /// <summary>
    /// 当前平台或运行环境不支持写入 Secret。
    /// </summary>
    Unavailable,

    /// <summary>
    /// 当前用户无权写入或删除 Secret。
    /// </summary>
    AccessDenied,

    /// <summary>
    /// 当前平台只有弱保护文件保存可用，但调用方没有显式允许。
    /// </summary>
    WeakProtectionNotAllowed,

    /// <summary>
    /// 后端发生其它错误。
    /// </summary>
    BackendError
}
