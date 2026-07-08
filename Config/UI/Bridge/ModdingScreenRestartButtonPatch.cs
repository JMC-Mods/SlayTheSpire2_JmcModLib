using Godot;
using HarmonyLib;
using JmcModLib.UI;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace JmcModLib.Config.UI;

[HarmonyPatch(typeof(NModdingScreen))]
internal static class ModdingScreenRestartButtonPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NModdingScreen._Ready))]
    private static void AfterReady(NModdingScreen __instance)
    {
        Refresh(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NModdingScreen.OnModEnabledOrDisabled))]
    private static void AfterModEnabledOrDisabled(NModdingScreen __instance)
    {
        Refresh(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NModdingScreen._ExitTree))]
    private static void BeforeExitTree(NModdingScreen __instance)
    {
        RestartConfirmButtonUi.Remove(__instance);
    }

    private static void Refresh(NModdingScreen screen)
    {
        if (!JmcModLibSettings.AutoShowModManagerRestartButton)
        {
            RestartConfirmButtonUi.SetVisible(screen, false);
            return;
        }

        Control? pendingChangesWarning = screen.GetNodeOrNull<Control>("%PendingChangesLabel");
        bool shouldShow = pendingChangesWarning?.Visible == true;
        RestartConfirmButtonUi.SetVisible(screen, shouldShow);
    }
}
