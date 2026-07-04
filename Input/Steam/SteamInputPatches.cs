using HarmonyLib;
using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Platform.Steam;

namespace JmcModLib.Input;

/// <summary>
/// Steam Input 初始化补丁：在游戏调用 SteamInput.Init 前准备 manifest，并在运行时修复原生手柄映射。
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
    private static readonly MemberAccessor? ControllerInputMapMember =
        TryGetMember(typeof(NInputManager), "_controllerInputMap");

    private static readonly MethodAccessor? EmitSignalInputReboundMethod =
        TryGetMethod(typeof(NInputManager), "EmitSignalInputRebound");

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

            if (ControllerInputMapMember == null
                || EmitSignalInputReboundMethod == null)
            {
                ModLogger.Warn("无法修复 Steam Input 手柄映射：游戏内部映射结构已变化。");
                return;
            }

            Dictionary<Godot.StringName, Godot.StringName> defaultMap = controllerManager.GetDefaultControllerInputMap;
            object? currentValue = ControllerInputMapMember.GetValue(inputManager);
            Dictionary<Godot.StringName, Godot.StringName> repairedMap = currentValue is Dictionary<Godot.StringName, Godot.StringName> currentMap
                ? new Dictionary<Godot.StringName, Godot.StringName>(currentMap)
                : [];

            KeyValuePair<Godot.StringName, Godot.StringName>[] invalidEntries =
            [
                .. repairedMap.Where(entry => !Godot.InputMap.HasAction(entry.Value))
            ];
            foreach (KeyValuePair<Godot.StringName, Godot.StringName> entry in invalidEntries)
            {
                if (defaultMap.TryGetValue(entry.Key, out Godot.StringName? defaultValue)
                    && defaultValue != null
                    && Godot.InputMap.HasAction(defaultValue))
                {
                    repairedMap[entry.Key] = defaultValue;
                    continue;
                }

                repairedMap.Remove(entry.Key);
            }

            KeyValuePair<Godot.StringName, Godot.StringName>[] missingEntries =
            [
                .. defaultMap.Where(entry => Godot.InputMap.HasAction(entry.Value) && !repairedMap.ContainsKey(entry.Key))
            ];
            if (invalidEntries.Length == 0 && missingEntries.Length == 0)
            {
                return;
            }

            foreach (KeyValuePair<Godot.StringName, Godot.StringName> entry in missingEntries)
            {
                repairedMap.Add(entry.Key, entry.Value);
            }

            ControllerInputMapMember.SetValue(inputManager, repairedMap);
            _ = EmitSignalInputReboundMethod.Invoke(inputManager);

            if (Interlocked.Exchange(ref repairedLogged, 1) == 0)
            {
                List<string> details = [];
                if (missingEntries.Length > 0)
                {
                    details.Add($"补全 {missingEntries.Length} 项");
                }

                if (invalidEntries.Length > 0)
                {
                    details.Add($"回退无效映射 {invalidEntries.Length} 项");
                }

                ModLogger.Info($"已修复 Steam Input 手柄映射（仅本次运行生效）：{controllerManager.ControllerMappingType}，{string.Join("，", details)}。");
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error("修复 Steam Input 手柄映射失败。", ex);
        }
    }

    private static MemberAccessor? TryGetMember(Type type, string memberName)
    {
        try
        {
            return MemberAccessor.Get(type, memberName);
        }
        catch (MissingMemberException)
        {
            return null;
        }
    }

    private static MethodAccessor? TryGetMethod(Type type, string methodName)
    {
        try
        {
            return MethodAccessor.Get(type, methodName);
        }
        catch (MissingMethodException)
        {
            return null;
        }
    }
}
