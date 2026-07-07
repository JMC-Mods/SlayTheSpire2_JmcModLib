namespace JmcModLib.Security;

/// <summary>
/// 子 MOD 持有的 Secret 槽位句柄。
/// </summary>
/// <remarks>
/// 本类型只保存槽位元数据，不保存密钥明文。读取到的 <see cref="string"/> 明文无法被 .NET 清零，
/// 调用方应避免记录日志、长时间缓存或传递到不可信位置。
/// </remarks>
public sealed class JmcSecretSlot
{
    private SecretRegistration? registration;

    /// <summary>
    /// 当前 Secret 的稳定键；未绑定时为空字符串。
    /// </summary>
    public string Key => registration?.Key ?? string.Empty;

    /// <summary>
    /// 当前 Secret 所属 MOD 的标识；未绑定时为空字符串。
    /// </summary>
    public string ModId => registration?.ModId ?? string.Empty;

    /// <summary>
    /// 当前运行时解析出的 Secret 范围；未绑定时为空字符串。
    /// </summary>
    public string Scope => registration?.ResolveScope() ?? string.Empty;

    /// <summary>
    /// 当前 Secret 槽位使用的保护等级。
    /// </summary>
    public JmcSecretProtectionLevel ProtectionLevel =>
        registration == null
            ? JmcSecretProtectionLevel.Unavailable
            : JmcSecretStore.GetProtectionLevel(registration.AllowWeakFileProtection);

    /// <summary>
    /// 尝试读取 Secret 明文。
    /// </summary>
    /// <param name="value">读取成功时返回 Secret 明文；失败时为空字符串。</param>
    /// <param name="status">读取结果状态。</param>
    /// <returns>读取成功时返回 <see langword="true"/>。</returns>
    public bool TryRead(out string value, out JmcSecretReadStatus status)
    {
        value = string.Empty;
        if (registration == null)
        {
            status = JmcSecretReadStatus.Unavailable;
            return false;
        }

        return JmcSecretStore.TryRead(registration, out value, out status);
    }

    /// <summary>
    /// 尝试保存 Secret 明文。
    /// </summary>
    /// <param name="value">要保存的 Secret 明文；不会写入普通配置 JSON。</param>
    /// <param name="status">写入结果状态。</param>
    /// <returns>保存成功时返回 <see langword="true"/>。</returns>
    public bool TrySave(string value, out JmcSecretWriteStatus status)
    {
        if (registration == null)
        {
            status = JmcSecretWriteStatus.Unavailable;
            return false;
        }

        return JmcSecretStore.TrySave(registration, value, out status);
    }

    /// <summary>
    /// 尝试删除当前 Secret。
    /// </summary>
    /// <param name="status">删除结果状态。</param>
    /// <returns>删除成功或目标已不存在时返回 <see langword="true"/>。</returns>
    public bool TryDelete(out JmcSecretWriteStatus status)
    {
        if (registration == null)
        {
            status = JmcSecretWriteStatus.Unavailable;
            return false;
        }

        return JmcSecretStore.TryDelete(registration, out status);
    }

    /// <summary>
    /// 判断当前 Secret 是否已保存。
    /// </summary>
    /// <returns>存在已保存 Secret 时返回 <see langword="true"/>。</returns>
    public bool Exists()
    {
        return registration != null && JmcSecretStore.Exists(registration);
    }

    internal void Bind(SecretRegistration nextRegistration)
    {
        ArgumentNullException.ThrowIfNull(nextRegistration);
        if (registration != null
            && (!ReferenceEquals(registration.Assembly, nextRegistration.Assembly)
                || !string.Equals(registration.Key, nextRegistration.Key, StringComparison.Ordinal)
                || !string.Equals(registration.Group, nextRegistration.Group, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Secret slot {registration.Key} 已绑定，不能重复绑定到 {nextRegistration.Key}。");
        }

        registration = nextRegistration;
    }
}
