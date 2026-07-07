using System.Text.Json.Serialization;

namespace JmcModLib.Security.Backends;

internal sealed class SecretFileDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("items")]
    public Dictionary<string, string> Items { get; set; } = new(StringComparer.Ordinal);
}
