namespace JmcModLib.UI.PauseMenu;

/// <summary>
/// 暂停菜单按钮相对于原生按钮的插入锚点。
/// </summary>
public enum PauseMenuButtonAnchor
{
    /// <summary>
    /// 插入到“继续”按钮之后。
    /// </summary>
    AfterResume = 0,

    /// <summary>
    /// 插入到“设置”按钮之后。
    /// </summary>
    AfterSettings = 1,

    /// <summary>
    /// 插入到“百科大全”按钮之后。
    /// </summary>
    AfterCompendium = 2,

    /// <summary>
    /// 插入到放弃、断开连接、保存并退出等离开运行的操作之前。
    /// </summary>
    BeforeExitActions = 3,

    /// <summary>
    /// 插入到暂停菜单按钮列表末尾。
    /// </summary>
    End = 4
}
