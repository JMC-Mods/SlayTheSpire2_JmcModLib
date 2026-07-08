using JmcModLib.Persistence.Storage;
using Newtonsoft.Json.Linq;

namespace JmcModLib.Persistence.Run;

internal sealed class RunPersistenceDocument
{
    public const string RootPropertyName = "_jml";

    private readonly JObject root;

    private RunPersistenceDocument(JObject root, long? startTime)
    {
        this.root = root;
        StartTime = startTime;
        EnsureShape(this.root);
    }

    public long? StartTime { get; }

    public static RunPersistenceDocument Empty(long? startTime = null)
    {
        return new RunPersistenceDocument(new JObject(), startTime);
    }

    public static RunPersistenceDocument FromSaveRoot(JObject saveRoot, long? startTime)
    {
        if (saveRoot[RootPropertyName] is JObject existing)
        {
            return new RunPersistenceDocument((JObject)existing.DeepClone(), startTime);
        }

        return Empty(startTime);
    }

    public RunPersistenceDocument Clone(long? startTime = null)
    {
        return new RunPersistenceDocument((JObject)root.DeepClone(), startTime ?? StartTime);
    }

    public JToken? GetValue(string modId, string storageKey)
    {
        string modKey = PersistenceIdentifier.SanitizeKey(modId);
        string key = PersistenceIdentifier.SanitizeKey(storageKey);
        return root["mods"]?[modKey]?[key]?["value"];
    }

    public void SetValue(string modId, string storageKey, PersistenceDocumentEntry entry)
    {
        string modKey = PersistenceIdentifier.SanitizeKey(modId);
        string key = PersistenceIdentifier.SanitizeKey(storageKey);
        JObject mods = EnsureObject(root, "mods");
        JObject modObject = EnsureObject(mods, modKey);
        modObject[key] = new JObject
        {
            ["schema_version"] = entry.SchemaVersion,
            ["value"] = entry.Value ?? JValue.CreateNull(),
        };
    }

    public JObject ToJObject()
    {
        EnsureShape(root);
        return (JObject)root.DeepClone();
    }

    private static void EnsureShape(JObject target)
    {
        target["version"] ??= 1;
        _ = EnsureObject(target, "mods");
    }

    private static JObject EnsureObject(JObject owner, string propertyName)
    {
        if (owner[propertyName] is JObject existing)
        {
            return existing;
        }

        var created = new JObject();
        owner[propertyName] = created;
        return created;
    }
}
