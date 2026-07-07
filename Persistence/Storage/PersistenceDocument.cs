using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JmcModLib.Persistence.Storage;

internal sealed class PersistenceDocument
{
    [JsonProperty("version")]
    public int Version { get; set; } = 1;

    [JsonProperty("values")]
    public Dictionary<string, PersistenceDocumentEntry> Values { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class PersistenceDocumentEntry
{
    [JsonProperty("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("value")]
    public JToken? Value { get; set; }
}
