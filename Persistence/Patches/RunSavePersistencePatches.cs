using HarmonyLib;
using JmcModLib.Persistence.Run;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace JmcModLib.Persistence.Patches;

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), new[] { typeof(SerializableRun), typeof(bool) })]
internal static class PersistenceRunSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        RunSaveManager __instance,
        SerializableRun save,
        bool isMultiplayer,
        ref Task __result)
    {
        __result = RunPersistenceManager.AppendPersistenceAfterOriginalSaveAsync(__instance, save, isMultiplayer, __result);
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave))]
internal static class PersistenceLoadRunSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(RunSaveManager __instance, ReadSaveResult<SerializableRun> __result)
    {
        RunPersistenceManager.LoadRunDocumentFromSave(__instance, isMultiplayer: false, __result);
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadMultiplayerRunSave))]
internal static class PersistenceLoadMultiplayerRunSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(RunSaveManager __instance, ReadSaveResult<SerializableRun> __result)
    {
        RunPersistenceManager.LoadRunDocumentFromSave(__instance, isMultiplayer: true, __result);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CanonicalizeSave), new[] { typeof(SerializableRun), typeof(ulong) })]
internal static class PersistenceCanonicalizeRunSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(SerializableRun save, SerializableRun __result)
    {
        RunPersistenceManager.CopyAttachedDocument(save, __result);
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.DeleteCurrentRun))]
internal static class PersistenceDeleteCurrentRunPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RunPersistenceManager.ClearRunContext();
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.DeleteCurrentMultiplayerRun))]
internal static class PersistenceDeleteCurrentMultiplayerRunPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RunPersistenceManager.ClearRunContext();
    }
}
