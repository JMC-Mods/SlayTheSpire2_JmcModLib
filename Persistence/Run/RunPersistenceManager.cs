using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace JmcModLib.Persistence.Run;

internal static class RunPersistenceManager
{
    private static readonly FieldInfo? SaveStoreField = AccessTools.Field(typeof(RunSaveManager), "_saveStore");
    private static readonly FieldInfo? ForceSynchronousField = AccessTools.Field(typeof(RunSaveManager), "_forceSynchronous");
    private static readonly FieldInfo? ProfileIdProviderField = AccessTools.Field(typeof(RunSaveManager), "_profileIdProvider");
    private static readonly FieldInfo? RunStartTimeField = AccessTools.Field(typeof(RunManager), "_startTime");

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly object SyncRoot = new();
    private static readonly ConditionalWeakTable<SerializableRun, AttachedRunDocument> AttachedDocuments = new();

    private static RunPersistenceDocument? currentDocument;
    private static int dirty;

    public static RunPersistenceDocument? CurrentDocument
    {
        get
        {
            lock (SyncRoot)
            {
                return currentDocument;
            }
        }
    }

    public static bool CanAccessRunData
    {
        get
        {
            try
            {
                return RunManager.Instance.IsInProgress;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool TryGetCurrentRunIdentity(out JmcRunIdentity identity)
    {
        identity = default;

        try
        {
            RunManager? runManager = RunManager.Instance;
            if (runManager?.IsInProgress != true)
            {
                return false;
            }

            if (RunStartTimeField?.GetValue(runManager) is not long startTime || startTime <= 0)
            {
                return false;
            }

            if (runManager.NetService == null)
            {
                return false;
            }

            int profileId = SaveManager.Instance.CurrentProfileId;
            bool isMultiplayer = runManager.NetService.Type.IsMultiplayer();
            identity = new JmcRunIdentity(profileId, startTime, isMultiplayer);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void MarkDirty()
    {
        Volatile.Write(ref dirty, 1);
    }

    public static void StartNewRun()
    {
        lock (SyncRoot)
        {
            currentDocument = RunPersistenceDocument.Empty();
            Volatile.Write(ref dirty, 0);
        }

        JmcPersistenceManager.ResetRunEntriesToDefault();
    }

    public static void ClearRunContext()
    {
        lock (SyncRoot)
        {
            currentDocument = null;
            Volatile.Write(ref dirty, 0);
        }

        JmcPersistenceManager.ResetRunEntriesToDefault();
    }

    public static void LoadRunDocumentFromSave(
        RunSaveManager runSaveManager,
        bool isMultiplayer,
        ReadSaveResult<SerializableRun>? result)
    {
        if (result?.Success != true || result.SaveData == null)
        {
            return;
        }

        try
        {
            if (!TryGetSaveRuntime(runSaveManager, isMultiplayer, out ISaveStore? saveStore, out string savePath, out _)
                || saveStore == null)
            {
                return;
            }

            string? json = saveStore.ReadFile(savePath);
            RunPersistenceDocument document = TryReadDocument(json, result.SaveData.StartTime)
                ?? RunPersistenceDocument.Empty(result.SaveData.StartTime);
            AttachDocument(result.SaveData, document);
            lock (SyncRoot)
            {
                currentDocument = document;
                Volatile.Write(ref dirty, 0);
            }

            JmcPersistenceManager.LoadRunEntries(document);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("读取 run save 中的 JML Persistence 数据失败。", ex);
        }
    }

    public static async Task AppendPersistenceAfterOriginalSaveAsync(
        RunSaveManager runSaveManager,
        SerializableRun save,
        bool isMultiplayer,
        Task originalSaveTask)
    {
        ArgumentNullException.ThrowIfNull(originalSaveTask);
        await originalSaveTask;

        try
        {
            if (!TryGetSaveRuntime(runSaveManager, isMultiplayer, out ISaveStore? saveStore, out string savePath, out bool forceSynchronous)
                || saveStore == null)
            {
                ModLogger.Warn("原版 run save 已完成，但 JML 未能解析游戏存档后端，已跳过本次 Persistence 附加写入。");
                return;
            }

            string? json = saveStore.ReadFile(savePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                ModLogger.Warn("原版 run save 已完成，但 JML 读取到的 run save 为空，已跳过本次 Persistence 附加写入。");
                return;
            }

            string injectedJson = InjectPersistenceJson(json, saveStore, savePath, save);
            if (string.Equals(injectedJson, json, StringComparison.Ordinal))
            {
                return;
            }

            byte[] bytes = Utf8NoBom.GetBytes(injectedJson);

            if (forceSynchronous)
            {
                saveStore.WriteFile(savePath, bytes);
            }
            else
            {
                await saveStore.WriteFileAsync(savePath, bytes);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn("原版 run save 已完成，但 JML 附加写入 Persistence 数据失败。", ex);
        }
    }

    public static void CopyAttachedDocument(SerializableRun source, SerializableRun target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (TryGetAttachedDocument(source, out RunPersistenceDocument? document) && document != null)
        {
            AttachDocument(target, document.Clone(target.StartTime));
        }
    }

    private static string InjectPersistenceJson(
        string json,
        ISaveStore saveStore,
        string savePath,
        SerializableRun save)
    {
        try
        {
            JObject saveRoot = JObject.Parse(json);
            RunPersistenceDocument document = ResolveDocumentForSave(save, saveStore, savePath);
            JmcPersistenceManager.CaptureRunEntries(document);
            saveRoot[RunPersistenceDocument.RootPropertyName] = document.ToJObject();
            AttachDocument(save, document);

            lock (SyncRoot)
            {
                currentDocument = document;
                Volatile.Write(ref dirty, 0);
            }

            return saveRoot.ToString(Formatting.None);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("写入 run save 扩展数据失败，本次将仅保存游戏原生 run 数据。", ex);
            return json;
        }
    }

    private static RunPersistenceDocument ResolveDocumentForSave(
        SerializableRun save,
        ISaveStore saveStore,
        string savePath)
    {
        long startTime = save.StartTime;
        lock (SyncRoot)
        {
            if (currentDocument != null
                && (!currentDocument.StartTime.HasValue || currentDocument.StartTime.Value == startTime))
            {
                return currentDocument;
            }

            if (TryGetAttachedDocument(save, out RunPersistenceDocument? attached)
                && attached != null
                && (!attached.StartTime.HasValue || attached.StartTime.Value == startTime))
            {
                currentDocument = attached;
                return attached;
            }
        }

        try
        {
            string? existingJson = saveStore.FileExists(savePath) ? saveStore.ReadFile(savePath) : null;
            RunPersistenceDocument? existing = TryReadDocument(existingJson, startTime);
            if (existing != null)
            {
                return existing;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn("读取既有 run save 扩展数据失败，将使用空扩展文档。", ex);
        }

        return RunPersistenceDocument.Empty(startTime);
    }

    private static void AttachDocument(SerializableRun save, RunPersistenceDocument document)
    {
        lock (SyncRoot)
        {
            AttachedDocuments.Remove(save);
            AttachedDocuments.Add(save, new AttachedRunDocument(document));
        }
    }

    private static bool TryGetAttachedDocument(SerializableRun save, out RunPersistenceDocument? document)
    {
        lock (SyncRoot)
        {
            if (AttachedDocuments.TryGetValue(save, out AttachedRunDocument? attached))
            {
                document = attached.Document;
                return true;
            }
        }

        document = null;
        return false;
    }

    private static RunPersistenceDocument? TryReadDocument(string? json, long? expectedStartTime)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        JObject root = JObject.Parse(json);
        long? startTime = root["start_time"]?.Value<long?>();
        if (expectedStartTime.HasValue
            && startTime.HasValue
            && startTime.Value != expectedStartTime.Value)
        {
            return null;
        }

        return RunPersistenceDocument.FromSaveRoot(root, startTime ?? expectedStartTime);
    }

    private static bool TryGetSaveRuntime(
        RunSaveManager runSaveManager,
        bool isMultiplayer,
        out ISaveStore? saveStore,
        out string savePath,
        out bool forceSynchronous)
    {
        saveStore = SaveStoreField?.GetValue(runSaveManager) as ISaveStore;
        forceSynchronous = ForceSynchronousField?.GetValue(runSaveManager) as bool? ?? false;
        savePath = string.Empty;

        object? profileIdProvider = ProfileIdProviderField?.GetValue(runSaveManager);
        object? profileValue = profileIdProvider?
            .GetType()
            .GetProperty(nameof(IProfileIdProvider.CurrentProfileId))?
            .GetValue(profileIdProvider);
        if (profileValue is not int profileId)
        {
            return false;
        }

        savePath = RunSaveManager.GetRunSavePath(
            profileId,
            isMultiplayer ? RunSaveManager.multiplayerRunSaveFileName : RunSaveManager.runSaveFileName);
        return saveStore != null;
    }

    private sealed class AttachedRunDocument(RunPersistenceDocument document)
    {
        public RunPersistenceDocument Document { get; } = document;
    }
}
