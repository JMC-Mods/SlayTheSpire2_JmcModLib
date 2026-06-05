using JmcModLib.Config.UI;

namespace JmcModLib.UI.PauseMenu;

/// <summary>
/// 描述一个暂停菜单按钮的显示、排序、本地化与运行时状态选项。
/// </summary>
public sealed class PauseMenuButtonOptions
{
    /// <summary>
    /// 创建一个空的暂停菜单按钮选项对象。
    /// </summary>
    public PauseMenuButtonOptions()
    {
    }

    /// <summary>
    /// 使用必需的键和回退文本创建暂停菜单按钮选项。
    /// </summary>
    /// <param name="key">按钮在当前 MOD 程序集内的稳定键。</param>
    /// <param name="text">按钮显示文本的回退值。</param>
    public PauseMenuButtonOptions(string key, string text)
    {
        Key = key;
        Text = text;
    }

    /// <summary>
    /// 按钮在当前 MOD 程序集内的稳定键。
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 按钮显示文本的回退值。
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 按钮插入锚点，默认插入到离开运行的危险操作之前。
    /// </summary>
    public PauseMenuButtonAnchor Anchor { get; set; } = PauseMenuButtonAnchor.BeforeExitActions;

    /// <summary>
    /// 同一锚点内的排序值，数值越小越靠前。
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 本地化表名；留空时使用 JML 默认设置界面表。
    /// </summary>
    public string? LocTable { get; set; }

    /// <summary>
    /// 按钮文本的显式本地化键；留空时使用 JML 约定键。
    /// </summary>
    public string? TextKey { get; set; }

    /// <summary>
    /// 运行时可见性判断；返回 <see langword="false"/> 时隐藏按钮。
    /// </summary>
    public Func<PauseMenuButtonContext, bool>? VisibleWhen { get; set; }

    /// <summary>
    /// 运行时启用状态判断；返回 <see langword="false"/> 时禁用按钮。
    /// </summary>
    public Func<PauseMenuButtonContext, bool>? EnabledWhen { get; set; }

    /// <summary>
    /// 点击回调触发后是否关闭暂停菜单，默认不关闭。
    /// </summary>
    public bool CloseMenuOnClick { get; set; }

    /// <summary>
    /// 按钮颜色风格。
    /// </summary>
    public UIButtonColor Color { get; set; } = UIButtonColor.Default;
}
