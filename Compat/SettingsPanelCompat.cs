using Godot;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Compat;

internal static class SettingsPanelCompat
{
    private static readonly Lazy<MethodAccessor?> SettingTipPositionMethod = new(() =>
        CompatMemberResolver.FindMethod(
            typeof(NSettingsScreen), isStatic: true, "GetSettingTipPosition", typeof(Control)));

    private static readonly Lazy<MemberAccessor?> LegacySettingTipsOffset = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(NSettingsScreen), isStatic: true, "settingTipsOffset"));

    internal static Vector2 GetSettingTipPosition(Control owner)
    {
        // 0.113 的原生方法同时处理从右向左的布局，旧版本继续使用原生偏移量。
        if (SettingTipPositionMethod.Value is { } method)
        {
            return method.InvokeStatic<Control, Vector2>(owner);
        }

        if (LegacySettingTipsOffset.Value?.GetValue(null) is Vector2 offset)
        {
            return owner.GlobalPosition + offset;
        }

        throw new MissingMemberException("当前游戏版本缺少设置悬浮提示的位置计算入口。");
    }

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
