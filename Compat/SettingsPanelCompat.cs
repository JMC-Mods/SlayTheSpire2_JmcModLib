using Godot;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Compat;

internal static class SettingsPanelCompat
{
    // 0.107.1–0.109.1 的 _firstControl 是 protected，JML 子类可以直接赋值；
    // 0.110 将它收紧为 private，只保留只读 DefaultFocusedControl，因此改用 JML 反射库写入。
    private static readonly Lazy<MemberAccessor?> FirstControlAccessor = new(() =>
        CompatMemberResolver.FindWritableMember(
            typeof(NSettingsPanel),
            isStatic: false,
            "_firstControl"));

    internal static void SetDefaultFocusedControl(NSettingsPanel panel, Control? control)
    {
        ArgumentNullException.ThrowIfNull(panel);

        try
        {
            MemberAccessor? accessor = FirstControlAccessor.Value;
            if (accessor == null)
            {
                ModLogger.Warn("当前游戏版本缺少 NSettingsPanel 默认焦点成员，MOD 设置页的方向导航入口无法更新。");
                return;
            }

            accessor.SetValue(panel, control);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("更新 MOD 设置页的默认焦点控件失败。", ex);
        }
    }
}
