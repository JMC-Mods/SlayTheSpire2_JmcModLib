using System.Reflection;
using JmcModLib.Persistence.Storage;

namespace JmcModLib.Persistence;

internal sealed class JmcDataRegistration
{
    public JmcDataRegistration(
        Assembly assembly,
        PersistenceScope scope,
        string key,
        int schemaVersion,
        JmcDataWritePolicy writePolicy,
        Type declaringType,
        string memberName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        Assembly = assembly;
        Scope = scope;
        Key = key.Trim();
        StorageKey = PersistenceIdentifier.SanitizeKey(Key);
        SchemaVersion = schemaVersion <= 0 ? 1 : schemaVersion;
        WritePolicy = writePolicy;
        DeclaringType = declaringType;
        MemberName = memberName.Trim();
    }

    public Assembly Assembly { get; }

    public PersistenceScope Scope { get; }

    public string Key { get; }

    public string StorageKey { get; }

    public int SchemaVersion { get; }

    public JmcDataWritePolicy WritePolicy { get; }

    public Type DeclaringType { get; }

    public string MemberName { get; }

    public string ModId => ModRegistry.GetModId(Assembly);

    public string SourceDescription => $"{DeclaringType.FullName}.{MemberName}";
}
