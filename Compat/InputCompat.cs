using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace JmcModLib.Compat;

internal static class InputCompat
{
    // 0.107.1–0.109.1 用 IsUsingController 表示当前由手柄驱动焦点；
    // 0.110 引入键盘专用导航后将其替换为语义更广的 IsUsingDirectionalNavigation。
    private static readonly Lazy<MemberAccessor?> DirectionalNavigationAccessor = new(() =>
        CompatMemberResolver.FindReadableMember(
            typeof(NControllerManager),
            isStatic: false,
            "IsUsingDirectionalNavigation",
            "IsUsingController"));

    internal static bool IsUsingDirectionalNavigation(NControllerManager? manager)
    {
        if (manager == null)
        {
            return false;
        }

        try
        {
            return DirectionalNavigationAccessor.Value?.GetValue(manager) is true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn("读取当前输入导航模式失败。", ex);
            return false;
        }
    }
}
