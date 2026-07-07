namespace JmcModLib.Security.Backends;

internal interface ISecretBackend
{
    string BackendId { get; }

    JmcSecretProtectionLevel ProtectionLevel { get; }

    bool IsAvailable { get; }

    bool TryRead(SecretIdentifier id, out byte[] value, out JmcSecretReadStatus status);

    bool TrySave(SecretIdentifier id, ReadOnlySpan<byte> value, out JmcSecretWriteStatus status);

    bool TryDelete(SecretIdentifier id, out JmcSecretWriteStatus status);

    bool Exists(SecretIdentifier id);
}
