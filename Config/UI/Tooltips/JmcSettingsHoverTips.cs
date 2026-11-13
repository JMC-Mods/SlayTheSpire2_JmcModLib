using Godot;
using JmcModLib.Compat;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using System.Reflection;

namespace JmcModLib.Config.UI;

internal static class JmcSettingsHoverTips
{
    private static readonly PropertyInfo? TitleProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Title), BindingFlags.Instance | BindingFlags.Public);

    public static void Attach(Control owner, string title, string description)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        if (owner.MouseFilter == Control.MouseFilterEnum.Ignore)
        {
            owner.MouseFilter = Control.MouseFilterEnum.Pass;
        }

        HoverTip tip = Create(title.Trim(), description.Trim());
        owner.Connect(Control.SignalName.MouseEntered, Callable.From(() => Show(owner, tip)));
        owner.Connect(Control.SignalName.MouseExited, Callable.From(() => Hide(owner)));
        owner.Connect(Control.SignalName.FocusEntered, Callable.From(() => Show(owner, tip)));
        owner.Connect(Control.SignalName.FocusExited, Callable.From(() => Hide(owner)));
        owner.Connect(Node.SignalName.TreeExiting, Callable.From(() => Hide(owner)));
    }

    private static HoverTip Create(string title, string description)
    {
        // 0.113 起 Id 只读。每个 owner 单独显示一条提示，原生去重仅作用于该提示集合，
        // 因此保留构造函数生成的 Id，不再写入已经移除的 setter。
        var tip = new HoverTip(new LocString("settings_ui", "FASTMODE"), description);

        SetPrivateProperty(ref tip, TitleProperty, title);
        return tip;
    }

    private static void Show(Control owner, HoverTip tip)
    {
        try
        {
            NHoverTipSet.Remove(owner);
            NHoverTipSet? hoverTipSet = NHoverTipSet.CreateAndShow(owner, tip);
            if (hoverTipSet != null)
            {
                hoverTipSet.GlobalPosition = SettingsPanelCompat.GetSettingTipPosition(owner);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn("显示原生设置悬浮提示失败。", ex);
        }
    }

    private static void Hide(Control owner)
    {
        try
        {
            NHoverTipSet.Remove(owner);
        }
        catch
        {
            // 场景退出时，原生悬浮提示容器可能已经释放。
        }
    }

    private static void SetPrivateProperty<TValue>(ref TValue value, PropertyInfo? property, object? propertyValue)
        where TValue : struct
    {
        if (property?.SetMethod == null)
        {
            return;
        }

        object boxed = value;
        property.SetValue(boxed, propertyValue);
        value = (TValue)boxed;
    }
}
