using JmcModLib.Config.UI;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace JmcModLib.UI.PauseMenu;

internal sealed class PauseMenuButtonEntry(
    Assembly assembly,
    string key,
    string text,
    PauseMenuButtonAnchor anchor,
    int order,
    string? locTable,
    string? textKey,
    Func<PauseMenuButtonContext, bool>? visibleWhen,
    Func<PauseMenuButtonContext, bool>? enabledWhen,
    bool closeMenuOnClick,
    UIButtonColor color,
    Func<PauseMenuButtonContext, Task> action)
{
    public Assembly Assembly { get; } = assembly;

    public string Key { get; } = key;

    public string Text { get; } = text;

    public PauseMenuButtonAnchor Anchor { get; } = anchor;

    public int Order { get; } = order;

    public string? LocTable { get; } = locTable;

    public string? TextKey { get; } = textKey;

    public Func<PauseMenuButtonContext, bool>? VisibleWhen { get; } = visibleWhen;

    public Func<PauseMenuButtonContext, bool>? EnabledWhen { get; } = enabledWhen;

    public bool CloseMenuOnClick { get; } = closeMenuOnClick;

    public UIButtonColor Color { get; } = color;

    public string ModId => ModRegistry.GetModId(Assembly);

    public string AssemblyName => Assembly.GetName().Name ?? string.Empty;

    public PauseMenuButtonOptions ToOptions()
    {
        return new PauseMenuButtonOptions(Key, Text)
        {
            Anchor = Anchor,
            Order = Order,
            LocTable = LocTable,
            TextKey = TextKey,
            VisibleWhen = VisibleWhen,
            EnabledWhen = EnabledWhen,
            CloseMenuOnClick = CloseMenuOnClick,
            Color = Color
        };
    }

    public PauseMenuButtonContext CreateContext(NPauseMenu menu, NButton button, IRunState? runState)
    {
        ModContext mod = ModRegistry.GetContext(Assembly)
            ?? new ModContext(
                Assembly,
                ModRegistry.GetModId(Assembly),
                ModRegistry.GetDisplayName(Assembly),
                ModRegistry.GetVersion(Assembly));
        return new PauseMenuButtonContext(mod, runState, menu, button);
    }

    public bool EvaluateVisible(PauseMenuButtonContext context)
    {
        if (VisibleWhen == null)
        {
            return true;
        }

        try
        {
            return VisibleWhen(context);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"暂停菜单按钮 {Key} 的可见性判断失败，按钮将被隐藏。", ex, Assembly);
            return false;
        }
    }

    public bool EvaluateEnabled(PauseMenuButtonContext context)
    {
        if (EnabledWhen == null)
        {
            return true;
        }

        try
        {
            return EnabledWhen(context);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"暂停菜单按钮 {Key} 的启用状态判断失败，按钮将被禁用。", ex, Assembly);
            return false;
        }
    }

    public Task InvokeAsync(PauseMenuButtonContext context)
    {
        return action(context);
    }
}
