using JmcModLib.Config.UI;

namespace JmcModLib.UI.PauseMenu;

/// <summary>
/// 将静态方法声明为运行中暂停菜单里的按钮条目。
/// </summary>
/// <param name="text">按钮显示文本的回退值；找不到本地化文本时使用。</param>
/// <remarks>
/// <para>
/// 本 Attribute 支持标记以下静态方法签名：
/// <c>void Method()</c>、<c>void Method(PauseMenuButtonContext)</c>、
/// <c>Task Method()</c> 与 <c>Task Method(PauseMenuButtonContext)</c>。
/// </para>
/// <para>
/// 同一 MOD 程序集内 <see cref="Key"/> 相同的按钮会互相替换；不同程序集可以使用相同键。
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PauseMenuButtonAttribute(string text) : Attribute
{
    /// <summary>
    /// 按钮显示文本的回退值。
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    /// 按钮在当前 MOD 程序集内的稳定键；留空时会根据声明方法生成。
    /// </summary>
    public string? Key { get; set; }

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
    /// 点击回调触发后是否关闭暂停菜单，默认不关闭。
    /// </summary>
    public bool CloseMenuOnClick { get; set; }

    /// <summary>
    /// 按钮颜色风格。
    /// </summary>
    public UIButtonColor Color { get; set; } = UIButtonColor.Default;
}
