using JmcModLib.Reflection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace JmcModLib.Persistence.Run;

internal static class RunPersistenceManager
{
    private static readonly MemberAccessor? SaveStoreAccessor = TryGetMemberAccessor(typeof(RunSaveManager), "_saveStore");
    private static readonly MemberAccessor? ForceSynchronousAccessor = TryGetMemberAccessor(typeof(RunSaveManager), "_forceSynchronous");
    private static readonly MemberAccessor? ProfileIdProviderAccessor = TryGetMemberAccessor(typeof(RunSaveManager), "_profileIdProvider");
    private static readonly MemberAccessor? RunStartTimeAccessor = TryGetMemberAccessor(typeof(RunManager), "_startTime");
    private static readonly MethodAccessor? GetRunSavePathAccessor = ResolveGetRunSavePathAccessor();
    private static readonly MemberAccessor? CurrentRunSavePathAccessor = TryGetMemberAccessor(typeof(RunSaveManager), "CurrentRunSavePath");
    private static readonly MemberAccessor? CurrentMultiplayerRunSavePathAccessor = TryGetMemberAccessor(typeof(RunSaveManager), "CurrentMultiplayerRunSavePath");

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly object SyncRoot = new();
    private static readonly ConditionalWeakTable<SerializableRun, AttachedRunDocument> AttachedDocuments = new();

    private static RunPersistenceDocument? currentDocument;
    private static RunIdentity? currentClientRunIdentity;
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

    public static bool HasClientRunContext
    {
        get
        {
            lock (SyncRoot)
            {
                return currentClientRunIdentity.HasValue;
            }
        }
    }

    public static bool TryGetCurrentClientRunIdentity(out RunIdentity identity)
    {
        lock (SyncRoot)
        {
            if (currentClientRunIdentity.HasValue)
            {
                identity = currentClientRunIdentity.Value;
                return true;
            }
        }

        identity = default;
        return false;
    }

    public static bool TryResolveCurrentRunIdentity(out RunIdentity identity)
    {
        return TryCreateIdentityFromCurrentRun(out identity);
    }

    public static void MarkDirty()
    {
        Volatile.Write(ref dirty, 1);
    }

    public static void StartNewRun()
    {
        RunIdentity? previousClientRunIdentity;
        lock (SyncRoot)
        {
            previousClientRunIdentity = currentClientRunIdentity;
            currentDocument = RunPersistenceDocument.Empty();
            Volatile.Write(ref dirty, 0);
        }

        JmcPersistenceManager.ResetRunEntriesToDefault();
        if (previousClientRunIdentity.HasValue)
        {
            DeleteClientRunData(previousClientRunIdentity.Value);
        }

        if (TryCreateIdentityFromCurrentRun(out RunIdentity identity))
        {
            SetClientRunContext(identity, deleteExistingFile: true);
        }
        else
        {
            ClearClientRunContext(deleteFile: false);
        }
    }

    public static void ClearRunContext()
    {
        JmcPersistenceManager.FlushClientRunEntries();

        lock (SyncRoot)
        {
            currentDocument = null;
            currentClientRunIdentity = null;
            Volatile.Write(ref dirty, 0);
        }

        JmcPersistenceManager.ResetRunEntriesToDefault();
        JmcPersistenceManager.ResetClientRunEntriesToDefault();
    }

    public static void ActivateClientRunContextFromSave(SerializableRun save, bool isMultiplayer)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (TryCreateIdentityFromSave(save, isMultiplayer, out RunIdentity identity))
        {
            SetClientRunContext(identity, deleteExistingFile: false);
        }
        else
        {
            ClearClientRunContext(deleteFile: false);
        }
    }

    public static async Task ActivateClientRunContextAfterSetupAsync(
        Task originalSetupTask,
        SerializableRun save,
        bool isMultiplayer)
    {
        ArgumentNullException.ThrowIfNull(originalSetupTask);
        await originalSetupTask;
        ActivateClientRunContextFromSave(save, isMultiplayer);
    }

    public static void DeleteCurrentClientRunData()
    {
        if (TryCreateIdentityFromCurrentRun(out RunIdentity identity))
        {
            DeleteClientRunData(identity);
            return;
        }

        lock (SyncRoot)
        {
            if (currentClientRunIdentity.HasValue)
            {
                identity = currentClientRunIdentity.Value;
            }
            else
            {
                return;
            }
        }

        DeleteClientRunData(identity);
    }

    public static void DeleteClientRunData(RunIdentity identity)
    {
        JmcPersistenceManager.DeleteClientRunData(identity);

        lock (SyncRoot)
        {
            if (currentClientRunIdentity == identity)
            {
                currentClientRunIdentity = null;
            }
        }
    }

    public static bool TryResolveSaveRunIdentity(
        RunSaveManager runSaveManager,
        bool isMultiplayer,
        out RunIdentity identity)
    {
        identity = default;

        try
        {
            if (!TryGetSaveRuntime(runSaveManager, isMultiplayer, out ISaveStore? saveStore, out string savePath, out _)
                || saveStore == null)
            {
                return false;
            }

            int? profileId = TryGetProfileId(runSaveManager);
            if (!profileId.HasValue)
            {
                return false;
            }

            string? json = saveStore.FileExists(savePath) ? saveStore.ReadFile(savePath) : null;
            if (TryReadStartTime(json, out long startTime))
            {
                identity = new RunIdentity(profileId.Value, startTime, isMultiplayer);
                return true;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn("读取待删除 run save 的客户端本局数据身份失败。", ex);
        }

        return false;
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

    private static void SetClientRunContext(RunIdentity identity, bool deleteExistingFile)
    {
        lock (SyncRoot)
        {
            currentClientRunIdentity = identity;
        }

        if (deleteExistingFile)
        {
            JmcPersistenceManager.DeleteClientRunData(identity);
        }

        JmcPersistenceManager.LoadClientRunEntries();
    }

    private static void ClearClientRunContext(bool deleteFile)
    {
        RunIdentity? identity;
        lock (SyncRoot)
        {
            identity = currentClientRunIdentity;
            currentClientRunIdentity = null;
        }

        if (deleteFile && identity.HasValue)
        {
            JmcPersistenceManager.DeleteClientRunData(identity.Value);
        }
        else
        {
            JmcPersistenceManager.ResetClientRunEntriesToDefault();
        }
    }

    private static bool TryCreateIdentityFromCurrentRun(out RunIdentity identity)
    {
        identity = default;

        try
        {
            RunManager? runManager = RunManager.Instance;
            if (runManager?.IsInProgress != true)
            {
                return false;
            }

            if (RunStartTimeAccessor?.GetValue(runManager) is not long startTime || startTime <= 0)
            {
                return false;
            }

            if (runManager.NetService == null)
            {
                return false;
            }

            int profileId = SaveManager.Instance.CurrentProfileId;
            bool isMultiplayer = runManager.NetService.Type.IsMultiplayer();
            identity = new RunIdentity(profileId, startTime, isMultiplayer);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateIdentityFromSave(
        SerializableRun save,
        bool isMultiplayer,
        out RunIdentity identity)
    {
        identity = default;

        try
        {
            if (save.StartTime <= 0)
            {
                return false;
            }

            int profileId = SaveManager.Instance.CurrentProfileId;
            identity = new RunIdentity(profileId, save.StartTime, isMultiplayer);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadStartTime(string? json, out long startTime)
    {
        startTime = 0;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        JObject root = JObject.Parse(json);
        startTime = root["start_time"]?.Value<long?>() ?? 0;
        return startTime > 0;
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
        saveStore = SaveStoreAccessor?.GetValue(runSaveManager) as ISaveStore;
        forceSynchronous = ForceSynchronousAccessor?.GetValue(runSaveManager) is bool forceSynchronousValue
            && forceSynchronousValue;
        savePath = string.Empty;

        string? resolvedSavePath = TryGetSavePath(runSaveManager, isMultiplayer);
        if (string.IsNullOrWhiteSpace(resolvedSavePath))
        {
            return false;
        }

        savePath = resolvedSavePath;
        return saveStore != null;
    }

    private static string? TryGetSavePath(RunSaveManager runSaveManager, bool isMultiplayer)
    {
        int? profileId = TryGetProfileId(runSaveManager);
        if (!profileId.HasValue)
        {
            return null;
        }

        string fileName = isMultiplayer
            ? RunSaveManager.multiplayerRunSaveFileName
            : RunSaveManager.runSaveFileName;
        if (TryInvokeGetRunSavePath(profileId.Value, fileName, out string? reflectedPath)
            && !string.IsNullOrWhiteSpace(reflectedPath))
        {
            return reflectedPath;
        }

        MemberAccessor? property = isMultiplayer
            ? CurrentMultiplayerRunSavePathAccessor
            : CurrentRunSavePathAccessor;
        if (property?.GetValue(runSaveManager) is string path
            && !string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Path.Combine(
            UserDataPathProvider.GetProfileDir(profileId.Value),
            UserDataPathProvider.SavesDir,
            fileName);
    }

    private static MemberAccessor? TryGetMemberAccessor(Type type, string memberName)
    {
        try
        {
            return MemberAccessor.Get(type, memberName);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"解析运行时成员失败：{type.FullName}.{memberName}", ex);
            return null;
        }
    }

    private static MethodAccessor? ResolveGetRunSavePathAccessor()
    {
        try
        {
            return MethodAccessor.Get(
                typeof(RunSaveManager),
                "GetRunSavePath",
                [typeof(int), typeof(string)]);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("解析 RunSaveManager.GetRunSavePath 失败，将使用后备路径。", ex);
            return null;
        }
    }

    private static bool TryInvokeGetRunSavePath(int profileId, string fileName, out string? savePath)
    {
        savePath = null;
        if (GetRunSavePathAccessor == null)
        {
            return false;
        }

        try
        {
            savePath = GetRunSavePathAccessor.InvokeStatic<int, string, string>(profileId, fileName);
            return !string.IsNullOrWhiteSpace(savePath);
        }
        catch (Exception ex)
        {
            ModLogger.Warn("反射调用 RunSaveManager.GetRunSavePath 失败，将使用后备路径。", ex);
            return false;
        }
    }

    private static int? TryGetProfileId(RunSaveManager runSaveManager)
    {
        object? profileIdProvider = ProfileIdProviderAccessor?.GetValue(runSaveManager);
        object? profileValue = profileIdProvider?
            .GetType()
            .GetProperty(nameof(IProfileIdProvider.CurrentProfileId))?
            .GetValue(profileIdProvider);
        return profileValue is int profileId ? profileId : null;
    }

    private sealed class AttachedRunDocument(RunPersistenceDocument document)
    {
        public RunPersistenceDocument Document { get; } = document;
    }
}
