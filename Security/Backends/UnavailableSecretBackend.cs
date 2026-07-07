namespace JmcModLib.Security.Backends;

internal sealed class UnavailableSecretBackend(JmcSecretWriteStatus saveStatus) : ISecretBackend
{
    public string BackendId => "unavailable";

    public JmcSecretProtectionLevel ProtectionLevel => JmcSecretProtectionLevel.Unavailable;

    public bool IsAvailable => false;

    public bool TryRead(SecretIdentifier id, out byte[] value, out JmcSecretReadStatus status)
    {
        value = [];
        status = JmcSecretReadStatus.Unavailable;
        return false;
    }

    public bool TrySave(SecretIdentifier id, ReadOnlySpan<byte> value, out JmcSecretWriteStatus status)
    {
        status = saveStatus;
        return false;
    }

    public bool TryDelete(SecretIdentifier id, out JmcSecretWriteStatus status)
    {
        status = saveStatus == JmcSecretWriteStatus.WeakProtectionNotAllowed
            ? JmcSecretWriteStatus.Unavailable
            : saveStatus;
        return false;
    }

    public bool Exists(SecretIdentifier id)
    {
        return false;
    }
}
