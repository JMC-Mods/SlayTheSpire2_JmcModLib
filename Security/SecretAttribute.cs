using JmcModLib.Config;

namespace JmcModLib.Security;

/// <summary>
/// 将静态 <see cref="JmcSecretSlot"/> 字段或属性声明为一个 Secret 槽位。
/// </summary>
/// <remarks>
/// Secret 槽位会显示在 JML 设置页中，但不会写入普通配置 JSON；保存、读取和删除都通过
/// <see cref="JmcSecretStore"/> 的独立后端完成。
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class SecretAttribute : Attribute
{
    /// <summary>
    /// 创建一个 Secret 声明。
    /// </summary>
    /// <param name="key">当前 MOD 内稳定的 Secret 键。</param>
    public SecretAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key.Trim();
    }

    /// <summary>
    /// 当前 MOD 内稳定的 Secret 键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 设置页中的分组名。
    /// </summary>
    public string Group { get; set; } = ConfigAttribute.DefaultGroup;

    /// <summary>
    /// 本地化表名；为空时使用默认设置页表。
    /// </summary>
    public string? LocTable { get; set; }

    /// <summary>
    /// 显示名称的回退文本。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 描述文本的回退文本。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 显示名称的本地化键。
    /// </summary>
    public string? DisplayNameKey { get; set; }

    /// <summary>
    /// 描述文本的本地化键。
    /// </summary>
    public string? DescriptionKey { get; set; }

    /// <summary>
    /// 设置或更新按钮文本的本地化键。
    /// </summary>
    public string? SetButtonTextKey { get; set; }

    /// <summary>
    /// 清空按钮文本的本地化键。
    /// </summary>
    public string? ClearButtonTextKey { get; set; }

    /// <summary>
    /// 分组名称的本地化键。
    /// </summary>
    public string? GroupKey { get; set; }

    /// <summary>
    /// 指向同一类型内静态无参 string 方法或属性的名称，用于动态区分 Secret 范围。
    /// </summary>
    public string? ScopeProvider { get; set; }

    /// <summary>
    /// 是否允许在没有系统安全存储时使用弱保护文件保存。
    /// </summary>
    public bool AllowWeakFileProtection { get; set; }

    /// <summary>
    /// 同组内排序值，数值越小越靠前。
    /// </summary>
    public int Order { get; set; }
}
