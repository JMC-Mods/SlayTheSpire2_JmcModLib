using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Platform.Steam;
using System.Reflection;

namespace JmcModLib.Input;

/// <summary>
/// Steam Input 初始化补丁：在游戏调用 SteamInput.Init 前准备 manifest，并恢复旧版原生手柄映射重置逻辑。
/// </summary>
[HarmonyPatch(typeof(SteamControllerInputStrategy), nameof(SteamControllerInputStrategy.Init))]
internal static class SteamInputPatches
{
    [HarmonyPrefix]
    public static void Prefix()
    {
        JmcSteamInputManifestInstaller.InstallBeforeSteamInputInit();
    }

    [HarmonyPostfix]
    public static void Postfix(ref Task __result)
    {
        __result = RestoreDefaultControllerMappingAsync(__result);
    }

    private static async Task RestoreDefaultControllerMappingAsync(Task originalTask)
    {
        await originalTask;
        if (!SteamInitializer.Initialized)
        {
            return;
        }

        SteamInputControllerMappingResetter.Restore();
    }
}

internal static class SteamInputControllerMappingResetter
{
    private static readonly FieldInfo? ControllerInputMapField =
        AccessTools.Field(typeof(NInputManager), "_controllerInputMap");

    private static readonly MethodInfo? SaveControllerInputMappingMethod =
        AccessTools.Method(typeof(NInputManager), "SaveControllerInputMapping");

    private static readonly MethodInfo? EmitSignalInputReboundMethod =
        AccessTools.Method(typeof(NInputManager), "EmitSignalInputRebound");

    private static int restoreLogged;

    public static void Restore()
    {
        try
        {
            NInputManager? inputManager = NInputManager.Instance;
            NControllerManager? controllerManager = inputManager?.ControllerManager;
            if (inputManager == null || controllerManager == null)
            {
                ModLogger.Warn("无法恢复原版 Steam Input 手柄映射：输入管理器尚未就绪。");
                return;
            }

            if (ControllerInputMapField == null
                || SaveControllerInputMappingMethod == null
                || EmitSignalInputReboundMethod == null)
            {
                ModLogger.Warn("无法恢复原版 Steam Input 手柄映射：游戏内部映射结构已变化。");
                return;
            }

            ControllerInputMapField.SetValue(
                inputManager,
                new Dictionary<Godot.StringName, Godot.StringName>(controllerManager.GetDefaultControllerInputMap));
            _ = SaveControllerInputMappingMethod.Invoke(inputManager, null);
            _ = EmitSignalInputReboundMethod.Invoke(inputManager, null);

            if (Interlocked.Exchange(ref restoreLogged, 1) == 0)
            {
                ModLogger.Info($"已按 STS2 0.103 原版逻辑重置 Steam Input 手柄映射：{controllerManager.ControllerMappingType}");
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error("恢复原版 Steam Input 手柄映射失败。", ex);
        }
    }
}
