using HarmonyLib;
using JmcModLib.Persistence.Run;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace JmcModLib.Persistence.Patches;

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.InitProfileId))]
internal static class PersistenceInitProfileIdPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        JmcPersistenceManager.ReloadProfileData();
    }
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SwitchProfileId))]
internal static class PersistenceSwitchProfileIdPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        JmcPersistenceManager.FlushProfileData();
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        JmcPersistenceManager.ReloadProfileData();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class PersistenceNewSingleplayerRunPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RunPersistenceManager.StartNewRun();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewMultiplayer))]
internal static class PersistenceNewMultiplayerRunPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RunPersistenceManager.StartNewRun();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class PersistenceRunCleanUpPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RunPersistenceManager.ClearRunContext();
    }
}
