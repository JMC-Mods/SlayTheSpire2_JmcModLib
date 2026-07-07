using JmcModLib.Config;

namespace JmcModLib.Security;

/// <summary>
/// 手动注册 Secret 槽位时使用的显示、分组和后端选项。
/// </summary>
public sealed class JmcSecretOptions
{
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
    /// 设置或更新按钮文本的回退文本。
    /// </summary>
    public string? SetButtonText { get; set; }

    /// <summary>
    /// 清空按钮文本的回退文本。
    /// </summary>
    public string? ClearButtonText { get; set; }

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
    /// 动态 Secret 范围提供器；返回值会参与存储隔离。
    /// </summary>
    public Func<string>? ScopeProvider { get; set; }

    /// <summary>
    /// 是否允许在没有系统安全存储时使用弱保护文件保存。
    /// </summary>
    public bool AllowWeakFileProtection { get; set; }

    /// <summary>
    /// 同组内排序值，数值越小越靠前。
    /// </summary>
    public int Order { get; set; }
}
