using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace JmcModLib.Persistence.Storage;

internal sealed class NewtonsoftPersistenceStorage
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly ConcurrentDictionary<string, PersistenceDocument> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> dirtyFiles = new(StringComparer.OrdinalIgnoreCase);

    public PersistenceDocument GetDocument(string filePath, Assembly assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return cache.GetOrAdd(filePath, path => LoadDocument(path, assembly));
    }

    public void MarkDirty(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        dirtyFiles[filePath] = 0;
    }

    public void Flush(string filePath, Assembly assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!dirtyFiles.ContainsKey(filePath))
        {
            return;
        }

        PersistenceDocument document = GetDocument(filePath, assembly);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        string json = JsonConvert.SerializeObject(document, Formatting.Indented, PersistenceJson.Settings);
        string tempFile = $"{filePath}.tmp";
        File.WriteAllText(tempFile, json, Utf8NoBom);
        File.Copy(tempFile, filePath, overwrite: true);
        File.Delete(tempFile);
        _ = dirtyFiles.TryRemove(filePath, out _);
    }

    public void Drop(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        _ = cache.TryRemove(filePath, out _);
        _ = dirtyFiles.TryRemove(filePath, out _);
    }

    private static PersistenceDocument LoadDocument(string filePath, Assembly assembly)
    {
        if (!File.Exists(filePath))
        {
            return new PersistenceDocument();
        }

        try
        {
            string json = File.ReadAllText(filePath, Utf8NoBom);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PersistenceDocument();
            }

            PersistenceDocument document = JsonConvert.DeserializeObject<PersistenceDocument>(json, PersistenceJson.Settings)
                ?? new PersistenceDocument();
            document.Values = new Dictionary<string, PersistenceDocumentEntry>(
                document.Values ?? new Dictionary<string, PersistenceDocumentEntry>(),
                StringComparer.Ordinal);
            return document;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"读取 Persistence 文件失败：{filePath}", ex, assembly);
            return new PersistenceDocument();
        }
    }
}
