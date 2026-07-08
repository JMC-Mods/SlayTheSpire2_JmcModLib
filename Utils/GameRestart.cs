using Godot;
using JmcModLib.Prefabs;
using MegaCrit.Sts2.Core.Nodes;
using System.Reflection;

namespace JmcModLib.Utils;

/// <summary>
/// 提供跨平台安全回退的游戏重启工具。
/// </summary>
/// <remarks>
/// 桌面平台会优先使用 Godot 的 <see cref="OS.SetRestartOnExit(bool, string[])"/>，
/// 并通过游戏自身的 <see cref="NGame.Quit"/> 退出流程保存设置和进度。Android、iOS 等
/// Godot 当前不支持自动重启的平台会返回失败，并保留给调用方显示提示。
/// </remarks>
public static class GameRestart
{
    private static readonly string[] DesktopFeatureNames = ["windows", "macos", "linux", "linuxbsd", "bsd"];
    private static readonly string[] DesktopOsNames = ["Windows", "macOS", "Linux", "FreeBSD", "NetBSD", "OpenBSD", "BSD"];
    private static int? mainThreadId;

    /// <summary>
    /// 获取当前平台是否支持由 JML 请求自动重启游戏。
    /// </summary>
    public static bool IsRestartSupported => IsDesktopPlatform() && !OS.HasFeature("editor");

    /// <summary>
    /// 安排游戏在正常退出后自动重启，并请求游戏执行原生退出流程。
    /// </summary>
    /// <param name="preserveCommandLineArguments">是否沿用当前进程的命令行参数启动新进程。</param>
    /// <param name="assembly">日志归属程序集；留空时自动推断调用方程序集。</param>
    /// <returns>成功安排重启并发出退出请求时返回 <see langword="true"/>；当前平台不支持或安排失败时返回 <see langword="false"/>。</returns>
    public static bool RequestRestart(bool preserveCommandLineArguments = true, Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(GameRestart));
        if (!TryScheduleRestart(preserveCommandLineArguments, resolvedAssembly))
        {
            return false;
        }

        RequestGameQuit(resolvedAssembly);
        return true;
    }

    /// <summary>
    /// 显示 JML 原生确认弹窗；用户确认后安排自动重启并退出游戏。
    /// </summary>
    /// <param name="preserveCommandLineArguments">是否沿用当前进程的命令行参数启动新进程。</param>
    /// <param name="assembly">日志归属程序集；留空时自动推断调用方程序集。</param>
    /// <returns>用户确认且重启请求已发出时返回 <see langword="true"/>；用户取消或当前平台不支持时返回 <see langword="false"/>。</returns>
    public static async Task<bool> ShowRestartConfirmationAsync(
        bool preserveCommandLineArguments = true,
        Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(GameRestart));
        bool confirmed = await JmcConfirmationPopup.ShowConfirmationAsync(
            GameRestartText.ConfirmTitle(),
            GameRestartText.ConfirmBody(),
            GameRestartText.ConfirmButton(),
            GameRestartText.CancelButton(),
            assembly: resolvedAssembly);

        if (!confirmed)
        {
            return false;
        }

        if (RequestRestart(preserveCommandLineArguments, resolvedAssembly))
        {
            return true;
        }

        await JmcConfirmationPopup.ShowMessageAsync(
            GameRestartText.UnsupportedTitle(),
            GameRestartText.UnsupportedBody(OS.GetName()),
            GameRestartText.CloseButton(),
            assembly: resolvedAssembly);
        return false;
    }

    /// <summary>
    /// 仅安排游戏在下次正常退出后自动重启，不主动退出游戏。
    /// </summary>
    /// <param name="preserveCommandLineArguments">是否沿用当前进程的命令行参数启动新进程。</param>
    /// <param name="assembly">日志归属程序集；留空时自动推断调用方程序集。</param>
    /// <returns>成功写入重启计划时返回 <see langword="true"/>；当前平台不支持或写入失败时返回 <see langword="false"/>。</returns>
    public static bool TryScheduleRestart(bool preserveCommandLineArguments = true, Assembly? assembly = null)
    {
        Assembly resolvedAssembly = AssemblyResolver.Resolve(assembly, typeof(GameRestart));
        if (!IsRestartSupported)
        {
            ModLogger.Warn($"当前平台不支持自动重启游戏：{OS.GetName()}", resolvedAssembly);
            return false;
        }

        try
        {
            string[] arguments = preserveCommandLineArguments ? OS.GetCmdlineArgs() : [];
            OS.SetRestartOnExit(true, arguments);
            bool scheduled = OS.IsRestartOnExitSet();
            if (!scheduled)
            {
                ModLogger.Warn("Godot 未接受自动重启计划。", resolvedAssembly);
            }

            return scheduled;
        }
        catch (Exception ex)
        {
            ModLogger.Warn("安排游戏自动重启失败。", ex, resolvedAssembly);
            return false;
        }
    }

    internal static void MarkMainThread()
    {
        mainThreadId ??= System.Environment.CurrentManagedThreadId;
    }

    private static bool IsDesktopPlatform()
    {
        string osName = OS.GetName();
        if (DesktopOsNames.Any(name => osName.Contains(name, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return DesktopFeatureNames.Any(OS.HasFeature);
    }

    private static void RequestGameQuit(Assembly assembly)
    {
        if (IsMarkedMainThread())
        {
            QuitGame(assembly);
            return;
        }

        Callable.From(() => QuitGame(assembly)).CallDeferred();
    }

    private static bool IsMarkedMainThread()
    {
        return mainThreadId.HasValue && mainThreadId.Value == System.Environment.CurrentManagedThreadId;
    }

    private static void QuitGame(Assembly assembly)
    {
        try
        {
            NGame? game = NGame.Instance;
            if (game != null && GodotObject.IsInstanceValid(game))
            {
                ModLogger.Info("已安排游戏重启，开始执行原生退出流程。", assembly);
                game.Quit();
                return;
            }

            if (Engine.GetMainLoop() is SceneTree tree)
            {
                ModLogger.Info("已安排游戏重启，NGame 不可用，直接请求 SceneTree 退出。", assembly);
                tree.Quit();
                return;
            }

            ModLogger.Warn("已安排游戏重启，但无法找到可用的退出入口。", assembly);
        }
        catch (Exception ex)
        {
            ModLogger.Error("请求游戏退出以完成重启时失败。", ex, assembly);
        }
    }
}
