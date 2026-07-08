using HarmonyLib;
using JmcModLib.Persistence.Run;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
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

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))]
internal static class PersistenceSavedSingleplayerRunPatch
{
    [HarmonyPostfix]
    private static void Postfix(SerializableRun save, ref Task __result)
    {
        __result = RunPersistenceManager.ActivateClientRunContextAfterSetupAsync(
            __result,
            save,
            isMultiplayer: false);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
internal static class PersistenceSavedMultiplayerRunPatch
{
    [HarmonyPostfix]
    private static void Postfix(LoadRunLobby lobby, ref Task __result)
    {
        __result = RunPersistenceManager.ActivateClientRunContextAfterSetupAsync(
            __result,
            lobby.Run,
            isMultiplayer: true);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
internal static class PersistenceRunEndedPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RunPersistenceManager.DeleteCurrentClientRunData();
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
