using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Platform.Steam;
using System.Reflection;

namespace JmcModLib.Input;

/// <summary>
/// Steam Input 初始化补丁：在游戏调用 SteamInput.Init 前准备 manifest，并补全缺失的原生手柄映射。
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
        __result = RepairDefaultControllerMappingAsync(__result);
    }

    private static async Task RepairDefaultControllerMappingAsync(Task originalTask)
    {
        await originalTask;
        if (!SteamInitializer.Initialized)
        {
            return;
        }

        SteamInputControllerMappingRepairer.Repair();
    }
}

internal static class SteamInputControllerMappingRepairer
{
    private static readonly FieldInfo? ControllerInputMapField =
        AccessTools.Field(typeof(NInputManager), "_controllerInputMap");

    private static readonly MethodInfo? SaveControllerInputMappingMethod =
        AccessTools.Method(typeof(NInputManager), "SaveControllerInputMapping");

    private static readonly MethodInfo? EmitSignalInputReboundMethod =
        AccessTools.Method(typeof(NInputManager), "EmitSignalInputRebound");

    private static int repairedLogged;

    public static void Repair()
    {
        try
        {
            NInputManager? inputManager = NInputManager.Instance;
            NControllerManager? controllerManager = inputManager?.ControllerManager;
            if (inputManager == null || controllerManager == null)
            {
                ModLogger.Warn("无法修复 Steam Input 手柄映射：输入管理器尚未就绪。");
                return;
            }

            if (ControllerInputMapField == null
                || SaveControllerInputMappingMethod == null
                || EmitSignalInputReboundMethod == null)
            {
                ModLogger.Warn("无法修复 Steam Input 手柄映射：游戏内部映射结构已变化。");
                return;
            }

            Dictionary<Godot.StringName, Godot.StringName> defaultMap = controllerManager.GetDefaultControllerInputMap;
            object? currentValue = ControllerInputMapField.GetValue(inputManager);
            Dictionary<Godot.StringName, Godot.StringName> repairedMap = currentValue is Dictionary<Godot.StringName, Godot.StringName> currentMap
                ? new Dictionary<Godot.StringName, Godot.StringName>(currentMap)
                : [];

            KeyValuePair<Godot.StringName, Godot.StringName>[] missingEntries =
            [
                .. defaultMap.Where(entry => !repairedMap.ContainsKey(entry.Key))
            ];
            if (missingEntries.Length == 0)
            {
                return;
            }

            foreach (KeyValuePair<Godot.StringName, Godot.StringName> entry in missingEntries)
            {
                repairedMap.Add(entry.Key, entry.Value);
            }

            ControllerInputMapField.SetValue(inputManager, repairedMap);
            _ = SaveControllerInputMappingMethod.Invoke(inputManager, null);
            _ = EmitSignalInputReboundMethod.Invoke(inputManager, null);

            if (Interlocked.Exchange(ref repairedLogged, 1) == 0)
            {
                ModLogger.Info($"已补全 Steam Input 手柄映射缺失项：{controllerManager.ControllerMappingType}，补全 {missingEntries.Length} 项。");
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error("修复 Steam Input 手柄映射失败。", ex);
        }
    }
}
